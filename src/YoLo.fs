[<AutoOpen>]
module YoLo

open System

module String =
    let split (separator: string) (s: string) =
        s.Split(separator, StringSplitOptions.TrimEntries)
        |> Array.filter (fun s -> s <> "")
    let contains (value: string) (s: string) = s.Contains(value)
    let toLower (s: string) = s.ToLower()