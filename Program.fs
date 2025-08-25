module GPhotoServer.Server

open System.Net
open System.Threading.Tasks
open LibGPhoto2.GPhoto2
open FsToolkit.ErrorHandling
open GPhotoServer.DTOs

open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.DependencyInjection

type Tree =
    | Node of string * Tree list
    | Tip of string

module App =
    open libgphoto2

    let cameras = ResizeArray<Camera>()
    let scanContext = new GPhoto2.GPContext()

    let detectCameras () =
        use portList = new GPhoto2.GPPortInfoList()
        use camAbilities = new GPhoto2.CameraAbilitiesList()
        portList.Load()
        camAbilities.Load(scanContext)

        let cameraList = camAbilities.Detect(portList)
        let detectedCameras =
            [ for i in 0..cameraList.Count-1 do
                cameraList.GetName(i),
                cameraList.GetValue(i)
                |> portList.LookupPath
                |> portList.GetInfo ]

        cameras.Clear()

        detectedCameras
        |> List.map (fun (name, portInfo) -> new Camera(name, PortInfo = portInfo))
        |> cameras.AddRange

    let triggerCapture cameraIndex =
        result {
            let! cam =
                cameras
                |> Seq.tryItem cameraIndex
                |> Result.ofOption "Camera not found"

            return cam.TriggerCapture()
        }

    let getConfig cameraIndex =
        let cam = cameras |> Seq.item cameraIndex
        cam.Config
        |> Seq.map (fun struct (path, w) -> CameraWidgetDTO.ofWidget path w)
        |> Seq.toArray

    let setRangeWidgetValue cameraIndex configPath value =
        let cam = cameras |> Seq.item cameraIndex
        let widget = cam.Config |> Seq.tryFind (fun struct (path, _) -> path = configPath)
        match widget with
        | None -> Error $"Config not found for path: \"{configPath}\""
        | Some struct (_, (:? RangeCameraWidget as w)) ->
            w.Value <- value
            Ok ()
        | Some struct (_, w) -> Error $"Not correct type of config: \"{w.Type}\""

    let setToggleWidgetValue cameraIndex configPath value =
        let cam = cameras |> Seq.item cameraIndex
        let widget = cam.Config |> Seq.tryFind (fun struct (path, _) -> path = configPath)
        match widget with
        | None -> Error $"Config not found for path: \"{configPath}\""
        | Some struct (_, (:? ToggleCameraWidget as w)) ->
            w.Value <- value
            Ok ()
        | Some struct (_, w) -> Error $"Not correct type of config: \"{w.Type}\""

    let setChoicesWidgetValue cameraIndex configPath value =
        let cam = cameras |> Seq.item cameraIndex
        let widget = cam.Config |> Seq.tryFind (fun struct (path, _) -> path = configPath)
        match widget with
        | None -> Error $"Config not found for path: \"{configPath}\""
        | Some struct (_, (:? ChoicesCameraWidget as w)) ->
            w.Value <- value
            Ok ()
        | Some struct (_, w) -> Error $"Not correct type of config: \"{w.Type}\""


module Handlers =
    let refreshCameraList context =
        App.detectCameras()
        context |> Response.ofJson (App.cameras |> Seq.mapi CameraDTO.ofCamera)

    let rangeWidgetValueSetter (query: RequestData) camIndex configPath =
        result {
            let! newValue = query.TryGetFloat "newValue" |> Result.ofOption (HttpStatusCode.BadRequest, "newValue query parameter not specified")
            do! App.setRangeWidgetValue camIndex configPath (float32 newValue)
                |> Result.mapError (fun err -> HttpStatusCode.InternalServerError, err)
        }

    let toggleWidgetValueSetter (query: RequestData) camIndex configPath =
        result {
            let! newValue = query.TryGetBoolean "newValue" |> Result.ofOption (HttpStatusCode.BadRequest, "newValue query parameter not specified")
            do! App.setToggleWidgetValue camIndex configPath newValue
                |> Result.mapError (fun err -> HttpStatusCode.InternalServerError, err)
        }

    let choicesWidgetValueSetter (query: RequestData) camIndex configPath =
        result {
            let! newValue = query.TryGetString "newValue" |> Result.ofOption (HttpStatusCode.BadRequest, "newValue query parameter not specified")
            do! App.setChoicesWidgetValue camIndex configPath newValue
                |> Result.mapError (fun err -> HttpStatusCode.InternalServerError, err)
        }

    let setWidgetValue setter camIndex context =
        result {
            let query = context |> Request.getQuery
            let! configPath =
                query.TryGetString "configPath"
                |> Result.ofOption (HttpStatusCode.BadRequest, "configPath query parameter not specified")

            do! setter query camIndex configPath

            return context |> Response.ofPlainText "Success"
        }
        |> Result.defaultWith (fun (errorCode, error) ->
            context
            |> Response.withStatusCode (int errorCode)
            |> Response.ofPlainText error
        )

    let takePhoto camIndex =
        App.triggerCapture camIndex |> ignore
        Response.ofPlainText "Capturing"
        // |> Task.catch
        // |> Task.map (function
        //     | Choice1Of2 () -> context |> Response.ofEmpty
        //     | Choice2Of2 err -> context |> Response.ofString Encoding.UTF8 err.Message
        // )
        // :> Task

[<Interface>]
type Client =
    abstract member NewCameraDetected: CameraDTO -> Task
    abstract member CameraCaptureDone: int -> Task

type ControllerHub() =
    inherit Hub<Client>()

    override this.OnConnectedAsync() =
        task {
            App.detectCameras()
            App.cameras |> Seq.iteri (fun cameraIdx camera ->
                camera
                |> CameraDTO.ofCamera cameraIdx
                |> this.Clients.Caller.NewCameraDetected
                |> ignore
            )
        }

    member this.Aaa() =
        Task.Run(fun () ->
            App.cameras |> Seq.iteri (fun cameraIdx camera ->
                camera
                |> CameraDTO.ofCamera cameraIdx
                |> this.Clients.Caller.NewCameraDetected
                |> ignore
            )
        )

    member this.SetWidgetRangeValue(cameraIdx, widgetPath, newValue) = App.setRangeWidgetValue cameraIdx widgetPath newValue |> Result.defaultWith (failwithf "Failed to set range value: %s")
    member this.SetWidgetToggleValue(cameraIdx, widgetPath, newValue) = App.setToggleWidgetValue cameraIdx widgetPath newValue |> Result.defaultWith (failwithf "Failed to set toggle value: %s")
    member this.SetWidgetChoiceValue(cameraIdx, widgetPath, newValue) = App.setChoicesWidgetValue cameraIdx widgetPath newValue |> Result.defaultWith (failwithf "Failed to set choice value: %s")

    member this.TriggerCapture(cameraIdx) =
        let captureDoneTask =
            App.triggerCapture cameraIdx
            |> Result.defaultWith (failwithf "Failed to trigger capture: %s")
        captureDoneTask |> Task.map (fun () -> this.Clients.All.CameraCaptureDone cameraIdx |> ignore)

    member this.RefreshCameraList() =
        App.detectCameras()
        App.cameras |> Seq.iteri (fun cameraIdx camera ->
            camera
            |> CameraDTO.ofCamera cameraIdx
            |> this.Clients.Caller.NewCameraDetected
            |> ignore
        )

[<EntryPoint>]
let main args =
    let wapp =
        WebApplication
            .CreateBuilder(args)
            .AddServices(fun _ services ->
                services.AddSignalR().AddJsonProtocol() |> ignore
                services.AddCors(_.AddDefaultPolicy(fun b ->
                    b.AllowAnyMethod()
                     .AllowAnyHeader()
                     .AllowCredentials()
                     .WithOrigins("http://192.168.0.32:5173")
                     .SetIsOriginAllowed(fun _ -> true)
                    |> ignore
                )) |> ignore
                services
            )
            .Build()

    wapp.UseRouting()
        .UseCors()
        .UseEndpoints(fun endpoints ->
            endpoints.MapHub<ControllerHub>("/hub") |> ignore
        )
        .UseFalco([
            get "/" (Response.ofPlainText "Hello World!")
            get "/refresh-cameras" Handlers.refreshCameraList
            mapGet "/trigger-capture/{cameraIdx:int}" _.GetInt("cameraIdx") Handlers.takePhoto
            mapPost "/config/{cameraIdx:int}/range" _.GetInt("cameraIdx") (Handlers.setWidgetValue Handlers.rangeWidgetValueSetter)
            mapPost "/config/{cameraIdx:int}/toggle" _.GetInt("cameraIdx") (Handlers.setWidgetValue Handlers.toggleWidgetValueSetter)
            mapPost "/config/{cameraIdx:int}/choices" _.GetInt("cameraIdx") (Handlers.setWidgetValue Handlers.choicesWidgetValueSetter)
        ])
        |> ignore

    wapp.Run(Response.withStatusCode 404 >> Response.ofPlainText "Not found")

    0
