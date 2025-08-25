[<AutoOpen>]
module Helpers

module Result =
    let inline ofOption error opt =
        match opt with
        | Some v -> Ok v
        | None -> Error error
