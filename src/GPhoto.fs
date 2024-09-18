module GPhoto

open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open System.Text.RegularExpressions

open FsToolkit.ErrorHandling

module Process =
    let exec (ct: CancellationToken) (fileName: string) (args: string seq) =
        let startInfo = ProcessStartInfo(
            FileName = fileName,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true // Try without this line
        )
        args |> Seq.iter startInfo.ArgumentList.Add

        let p = new Process()
        p.StartInfo <- startInfo
        p.EnableRaisingEvents <- true
        p.Start() |> ignore

        ct.Register(fun () -> p.Close()) |> ignore

        p

    let getOutput ct (proc: Process) =
        task {
            let! res =
                [ proc.StandardOutput.ReadToEndAsync(ct)
                  proc.StandardError.ReadToEndAsync(ct) ]
                |> Task.WhenAll
                |> Task.map (fun arr ->
                    let output = arr[0]
                    let error = arr[1]

                    match error = "" with
                    | true -> Ok output
                    | false -> Error error
                )

            do! proc.WaitForExitAsync(ct)
            proc.Close()
            proc.Dispose()
            return res
        }


type Camera =
    { Model: string; Port: string }
    static member isNikon camera =
        camera.Model |> String.toLower |> String.contains "nikon"

let execGPhoto ct camera args =
    Seq.append
        [ "--port"; camera.Port ]
        args
    |> Process.exec ct "gphoto2"
    |> Process.getOutput ct

let autoDetectCameras ct =
    taskResult {
        let! output =
            Process.exec ct "gphoto2" [ "--auto-detect" ]
            |> Process.getOutput ct
            |> TaskResult.mapError (sprintf "Failed to auto detect cameras: %s")

        let lines =
            output
            |> String.split "\n"
            |> Array.skip 2

        let cameras =
            lines |> Array.map (fun line ->
                let columns = line |> String.split "  "
                { Model = columns[0]
                  Port = columns[1] }
            )

        return cameras
    }

let getMaximumShots ct camera =
    let getMaximumShots () =
        [ "--get-config"; "maximumshots" ]
        |> execGPhoto ct camera

    let getContinousShootingCount () =
        [ "--get-config"; "continousshootingcount" ]
        |> execGPhoto ct camera

    getMaximumShots()
    |> Task.bind (function
        | Ok res -> res |> Ok |> Task.singleton
        | Error _ -> getContinousShootingCount ()
    )
    |> TaskResult.mapError (sprintf "Failed to get maximum shots: %s")
    |> Task.map (Result.bind (fun output ->
        output
        |> String.split "\n"
        |> Array.tryPick (fun line ->
            let m = Regex.Match(line, "Current: (\d+)")
            match m.Success with
            | false -> None
            | true ->
                m.Groups
                |> Seq.tryItem 1
                |> Option.map (_.Value >> int)
        )
        |> Result.requireSome "Failed to parse maximum shots"
    ))

let captureImage ct camera =
    taskResult {
        let! maximumShots =
            getMaximumShots ct camera
            |> TaskResult.mapError (sprintf "Failed to get maximum shots: %s")

        do! maximumShots > 0
            |> Result.requireTrue "No shots left, try again later"

        do! [ "--filename"; "%Y-%m-%d/%H-%M-%S.%C"
              "--capture-image" ]
            |> execGPhoto ct camera
            |> TaskResult.mapError (sprintf "Failed to capture image: %s")
            |> TaskResult.ignore
    }
    |> TaskResult.ignore

let triggerCapture ct camera =
    taskResult {
        let! maximumShots =
            getMaximumShots ct camera
            |> TaskResult.mapError (sprintf "Failed to get maximum shots: %s")

        do! maximumShots > 0
            |> Result.requireTrue "No shots left, try again later"

        do! [ "--trigger-capture" ]
            |> execGPhoto ct camera
            |> TaskResult.mapError (sprintf "Failed to to trigger capture: %s")
            |> TaskResult.ignore
    }

let setCaptureTargetToMemoryCard ct camera =
    taskResult {
        let! captureTargets =
            [ "--get-config"; "capturetarget" ]
            |> execGPhoto ct camera
            |> TaskResult.mapError (sprintf "Failed to get capture target: %s")

        let! captureTarget =
            captureTargets
            |> String.split "\n"
            |> Array.tryPick (fun line ->
                let m = Regex.Match(line, "Choice: (\d)+ Memory card")
                match m.Success with
                | false -> None
                | true ->
                    m.Groups
                    |> Seq.tryItem 1
                    |> Option.map (_.Value >> int)
            )
            |> Result.requireSome (
                sprintf
                    "Failed to parse capture target or can't find memory card capture target:\n%s"
                    captureTargets
            )

        return
            [ "--set-config"
              sprintf "capturetarget=%i" captureTarget ]
            |> execGPhoto ct camera
            |> TaskResult.mapError (sprintf "Failed to set capture target to memory card: %s")
    }
    |> TaskResult.ignore
