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

        ct.Register(fun () ->
            p.Kill()
            p.Close()
            p.Dispose()
        ) |> ignore

        p.StandardInput.AutoFlush <- false
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


type Shell(camera: Camera, ?sourceCt: CancellationToken) =
    let cts = new CancellationTokenSource()
    let ct = cts.Token

    do
        match sourceCt with
        | None -> ()
        | Some sourceCt ->
            sourceCt.Register(fun _ -> cts.Cancel())
            |> ignore

    let proc =
        [ "--port"; camera.Port; "--shell" ]
        |> Process.exec ct "gphoto2"

    let waitCommandEnd (ct: CancellationToken) debug =
        task {
            let tcs = new TaskCompletionSource<string>()
            let mutable str = ""

            if debug then printfn ""
            while
                not proc.StandardOutput.EndOfStream
                && not ct.IsCancellationRequested
                && not tcs.Task.IsCompleted
                do
                let buffer = new System.Memory<_>(Array.zeroCreate 1)
                let! readCount = proc.StandardOutput.ReadAsync(buffer, ct)

                if readCount > 0 then
                    let char = buffer.ToArray() |> Array.head
                    str <- str + (string char)

                    if debug then printfn "%c" char
                    if debug then printfn "%A" str

                    let isEnd =
                        try
                            str
                            |> String.split System.Environment.NewLine
                            |> Seq.tail
                            |> Seq.tryFind (String.contains "/>")
                            |> Option.isSome
                        with _ -> false

                    if debug then printfn "%A" isEnd
                    match isEnd with
                    | false -> ()
                    | true -> tcs.SetResult str

            return! tcs.Task
        }

    interface System.IDisposable with
        override _.Dispose (): unit =
            cts.Cancel()
    member this.Dispose() = (this: System.IDisposable).Dispose()

    member private _.ExecuteCmd(command: string, ?debug: bool) =
        if ct.IsCancellationRequested then
            "Cancelled"
            |> Error
            |> Task.singleton
        else
            proc.StandardInput.WriteLine(command)
            proc.StandardInput.Flush()

            let task = waitCommandEnd ct (debug |> Option.defaultValue false)

            task.WaitAsync(new System.TimeSpan(0, 0, 10), ct)
            |> Task.catch
            |> Task.map (function
                | Choice1Of2 str ->
                    match str.ToLower().Contains "error" with
                    | true -> Error str
                    | false -> Ok str
                | Choice2Of2 exn -> Error exn.Message
            )

    member this.GetShotBuffer() =
        taskResult {
            let! maxShotsStr =
                this.ExecuteCmd("get-config maximumshots", false)
                |> TaskResult.orElseWith (fun _ ->
                    this.ExecuteCmd("get-config continousshootingcount", false)
                )
                |> TaskResult.mapError (sprintf "Failed to get max shots: %s")

            return!
                maxShotsStr
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
                |> Result.requireSome (sprintf "Failed to parse maximum shots: %s" maxShotsStr)
        }

    member this.TriggerCapture() =
        taskResult {
            let! maximumShots =
                this.GetShotBuffer()
                |> TaskResult.mapError (sprintf "Failed to get maximum shots: %s")

            do! maximumShots > 0
                |> Result.requireTrue "No shots left, try again later"

            do! this.ExecuteCmd("trigger-capture", false)
                |> TaskResult.mapError (sprintf "Failed to to trigger capture: %s")
                |> TaskResult.ignore
        }

    member this.CaptureImage() =
        taskResult {
            let! maximumShots =
                this.GetShotBuffer()
                |> TaskResult.mapError (sprintf "Failed to get maximum shots: %s")

            do! maximumShots > 0
                |> Result.requireTrue "No shots left, try again later"

            do! this.ExecuteCmd("filename %Y-%m-%d/%H-%M-%S.%C", false)
                |> TaskResult.mapError (sprintf "Failed to set image name: %s")
                |> TaskResult.ignore

            do! this.ExecuteCmd("capture-image", false)
                |> TaskResult.mapError (sprintf "Failed to capture image: %s")
                |> TaskResult.ignore
        }

    member this.SetCaptureTargetToMemoryCard() =
        taskResult {
            let! captureTargets =
                "get-config capturetarget"
                |> this.ExecuteCmd
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

            do!
                sprintf "set-config capturetarget=%i" captureTarget
                |> this.ExecuteCmd
                |> TaskResult.mapError (sprintf "Failed to set capture target to memory card: %s")
                |> TaskResult.ignore
        }


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
