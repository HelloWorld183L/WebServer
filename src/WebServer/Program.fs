﻿// Learn more about F# at http://fsharp.org

open System
open System.Net.Sockets
open SocketWrapper

let rec serverLoop() =
    
    serverLoop()

[<EntryPoint>]
let main argv =
    setupSocket()
    serverLoop()
    0 // return an integer exit code
