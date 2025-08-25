namespace rec GPhotoServer.DTOs

open System.Text.Json
open System.Text.Json.Serialization
open LibGPhoto2.GPhoto2

type Serializable =
    abstract member Serialize: Utf8JsonWriter -> unit

[<JsonDerivedType(typeof<ToggleCameraWidgetDTO>, "toggle")>]
[<JsonDerivedType(typeof<RangeCameraWidgetDTO>, "range")>]
[<JsonDerivedType(typeof<ChoicesCameraWidgetDTO>, "choices")>]
[<JsonDerivedType(typeof<TextCameraWidgetDTO>, "text")>]
type CameraWidgetDTO(label, path) =
    member _.Label: string = label
    member _.Path: string = path

    static member ofWidget (path: string) (widget: CameraWidget) : CameraWidgetDTO =
        match widget with
        | :? ToggleCameraWidget as w -> ToggleCameraWidgetDTO(w.Label, path, w)
        | :? RangeCameraWidget as w -> RangeCameraWidgetDTO(w.Label, path, w)
        | :? ChoicesCameraWidget as w -> ChoicesCameraWidgetDTO(w.Label, path, w)
        | :? TextCameraWidget as w -> TextCameraWidgetDTO(w.Label, path, w)
        | _ -> CameraWidgetDTO(widget.Label, path)

type ToggleCameraWidgetDTO(label, path, value: bool) =
    inherit CameraWidgetDTO(label, path)
    new(label, path, w: ToggleCameraWidget) = ToggleCameraWidgetDTO(label, path, w.Value)
    member _.Value = value

type RangeCameraWidgetDTO(label, path, min, max, increment) =
    inherit CameraWidgetDTO(label, path)

    new(label, path, w: RangeCameraWidget) =
        let min, max, incr = w.GetRange()
        RangeCameraWidgetDTO(label, path, min, max, incr)

    member _.Min = min
    member _.Max = max
    member _.Increment = increment

type ChoicesCameraWidgetDTO(label, path, choices, value) =
    inherit CameraWidgetDTO(label, path)
    new(label, path, w: ChoicesCameraWidget) = ChoicesCameraWidgetDTO(label, path, w.Choices |> Seq.toArray, w.Value)
    member _.Choices: string array = choices
    member _.Value: string = value

type TextCameraWidgetDTO(label, path, value: string) =
    inherit CameraWidgetDTO(label, path)
    new(label, path, w: TextCameraWidget) = TextCameraWidgetDTO(label, path, w.Value)
    member _.Value: string = value

type CameraDTO =
    { Name: string
      Widgets: CameraWidgetDTO array
      Id: int }

    static member ofCamera cameraIdx (camera: Camera) =
        { Name = camera.Name
          Id = cameraIdx
          Widgets =
              camera.Config
              |> Seq.map (fun struct (path, w) -> CameraWidgetDTO.ofWidget path w)
              |> Seq.toArray }
