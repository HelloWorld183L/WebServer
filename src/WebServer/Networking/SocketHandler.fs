module SocketHandler

open NetworkingTypes
open System.Net.Sockets

let disconnect socketState =
    socketState.Socket.Dispose()

let handleSocketError (clientSocket : Client) (socketError : SocketError) =
    printfn "Socket error! Message: %s" (socketError.ToString())

    clientSocket.ReceiveBuffer <- Array.zeroCreate 1024
    clientSocket.Socket.Dispose()