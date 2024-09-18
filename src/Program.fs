module GPhotoServer.Program

open Falco
open Falco.Routing
open Falco.HostBuilder
open Microsoft.AspNetCore.Http

open FsToolkit.ErrorHandling
open System.Threading.Tasks
open System.Threading
open System
open System.Collections.Generic

// --- Falco helpers ---
let trHandlerToHandler ctx tr =
    task {
        match! tr with
        | Ok response ->
            return! ctx |> Response.ofPlainText response
        | Error e ->
            return!
                ctx
                |> Response.withStatusCode 500
                |> Response.ofPlainText e

        return ()
    } :> Task

// --- Domain logic ---

let getCamera ct =
    taskResult {
        let! cams = GPhoto.autoDetectCameras ct
        do! cams |> Result.requireNotEmpty "No camera found"

        let! cam =
            cams
            |> Array.tryFind GPhoto.Camera.isNikon
            |> Result.requireSome "No Nikon camera found"

        return cam
    }

let intervalCapture ct (interval: TimeSpan) count camera =
    task {
        let mutable shotsDone = 0

        while shotsDone < count do
            printfn "Capture"
            let! res = camera |> GPhoto.triggerCapture ct
            printfn "Captured"
            match res with
            | Ok _ ->
                printfn "Done"
                shotsDone <- shotsDone + 1
                do! Task.Delay(interval, ct)
            | Error err ->
                printfn "Error while interval capture: %s" err
                do! Task.Delay(TimeSpan.FromMilliseconds(100), ct) // Wait before retrying
    }

// --- Handlers ---
// module State =
    // let tryGetCameraShell (camera: GPhoto.Camera) (ctx: HttpContext) =
    //     sprintf "shells:%s" camera.Port
    //     |> ctx.Items.TryGetValue
    //     |> function
    //         | true, :? GPhoto.Shell as shell ->
    //             shell
    //             |> unbox<GPhoto.Shell>
    //             |> Some
    //         | _ -> None

    // let createCameraShell (camera: GPhoto.Camera) (ctx: HttpContext) =
    //     let shell =
    //         camera
    //         |> GPhoto.Shell.openShellProcess CancellationToken.None
    //         |> GPhoto.Shell.fromProcess

    //     let itemId = sprintf "shells:%s" camera.Port |> box
    //     let itemValue = shell |> box

    //     KeyValuePair.Create(itemId, itemValue)
    //     |> ctx.Items.Add

    //     shell

    // let getCameraShell (camera: GPhoto.Camera) (ctx: HttpContext) =
    //     ctx
    //     |> tryGetCameraShell camera
    //     |> Option.defaultWith (fun _ -> createCameraShell camera ctx)

let rootHandler (ctx: HttpContext) =
    getCamera ctx.RequestAborted
    |> TaskResult.map (fun cam -> sprintf "Found camera: %s" cam.Model)
    |> trHandlerToHandler ctx

let setCaptureTargetHandler (ctx: HttpContext) =
    taskResult {
        let! camera = getCamera ctx.RequestAborted
        do! camera |> GPhoto.setCaptureTargetToMemoryCard CancellationToken.None

        return "Capture target set to memory card"
    }
    |> trHandlerToHandler ctx

let captureHandler (ctx: HttpContext) =
    taskResult {
        let! camera = getCamera ctx.RequestAborted
        do! camera
            |> GPhoto.triggerCapture CancellationToken.None

        return "Image capturing"
    }
    |> trHandlerToHandler ctx

let captureAndWaitHandler (ctx: HttpContext) =
    taskResult {
        let! camera = getCamera ctx.RequestAborted
        do! camera |> GPhoto.captureImage CancellationToken.None

        return "Image captured"
    }
    |> trHandlerToHandler ctx

let intervalCaptureHandler (ctx: HttpContext) =
    taskResult {
        let query = Request.getQuery ctx
        let! count =
            query.TryGetInt "count"
            |> Result.requireSome "Missing 'count' query parameter"

        let interval =
            query.TryGetInt "interval"
            |> Option.defaultValue 0
            |> TimeSpan.FromSeconds

        let! camera = getCamera ctx.RequestAborted

        // Start capturing images at intervals
        camera
        |> intervalCapture
            CancellationToken.None
            interval
            count
        |> ignore

        return "Started capturing images"
    }
    |> trHandlerToHandler ctx


[<EntryPoint>]
let main args =
    webHost args {
        endpoints [
            get "/" rootHandler
            get "/set-capture-target" setCaptureTargetHandler
            get "/capture" captureHandler
            get "/capture-and-wait" captureAndWaitHandler
            get "/capture-interval" intervalCaptureHandler
        ]
    }
    0