module SocketHandler

open NetworkingTypes

let disconnect socketState =
    socketState.SendBuffer <- Array.zeroCreate 0
    socketState.ReceiveBuffer <- Array.zeroCreate 0
    socketState.Socket.Dispose()