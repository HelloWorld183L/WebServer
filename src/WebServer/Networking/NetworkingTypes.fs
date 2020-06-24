module NetworkingTypes

open System.Net.Sockets

type HttpRequestInfo =
    {
        RequestMethod: string
        ResourceName: string
    }

type Client =
    {
        Socket: Socket
        mutable ReceiveBuffer: byte[]
    }