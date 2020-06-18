// Learn more about F# at http://fsharp.org

open SocketWrapper

[<EntryPoint>]
let main argv =
    startListening()
    0 // return an integer exit code
