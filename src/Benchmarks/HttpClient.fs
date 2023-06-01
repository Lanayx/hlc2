namespace Benchmarks

open System.Net.Http
open System
open BenchmarkDotNet.Attributes
open System.Threading.Tasks


type HttpClientBenchmarks() =
    member val simpleHttpClient = new HttpClient()
    member val clientWithBaseUrl = new HttpClient(BaseAddress = new Uri("http://192.168.0.101:30224"))


    //[<Benchmark>]
    //member this.SimpleHttpClient() =
    //    task {
    //        let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/get")
    //        let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/ip")
    //        let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/user-agent")
    //        let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/headers")
    //        let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/200")
    //        let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/201")
    //        let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/202")
    //        let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/203")
    //        let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/204")
    //        let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/205")
    //        return message
    //    }

    [<Benchmark>]
    member this.SimpleHttpClientParallel() =
        [|
            this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/get") :> Task
            this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/ip") :> Task
            this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/user-agent") :> Task
            this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/headers") :> Task
            this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/200") :> Task
            this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/201") :> Task
            this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/202") :> Task
            this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/203") :> Task
            this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/204") :> Task
            this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/205") :> Task
        |] |> Task.WaitAll



    //[<Benchmark>]
    //member this.BaseHttpClient() =
    //    task {
    //        let! message = this.clientWithBaseUrl.GetAsync("/get")
    //        let! message = this.clientWithBaseUrl.GetAsync("/ip")
    //        let! message = this.clientWithBaseUrl.GetAsync("/user-agent")
    //        let! message = this.clientWithBaseUrl.GetAsync("/headers")
    //        let! message = this.clientWithBaseUrl.GetAsync("/status/200")
    //        let! message = this.clientWithBaseUrl.GetAsync("/status/201")
    //        let! message = this.clientWithBaseUrl.GetAsync("/status/202")
    //        let! message = this.clientWithBaseUrl.GetAsync("/status/203")
    //        let! message = this.clientWithBaseUrl.GetAsync("/status/204")
    //        let! message = this.clientWithBaseUrl.GetAsync("/status/205")
    //        return message
    //    }

    [<Benchmark>]
    member this.BaseHttpClientParallel() =
        [|
            this.clientWithBaseUrl.GetAsync("/get") :> Task
            this.clientWithBaseUrl.GetAsync("/ip") :> Task
            this.clientWithBaseUrl.GetAsync("/user-agent") :> Task
            this.clientWithBaseUrl.GetAsync("/headers") :> Task
            this.clientWithBaseUrl.GetAsync("/status/200") :> Task
            this.clientWithBaseUrl.GetAsync("/status/201") :> Task
            this.clientWithBaseUrl.GetAsync("/status/202") :> Task
            this.clientWithBaseUrl.GetAsync("/status/203") :> Task
            this.clientWithBaseUrl.GetAsync("/status/204") :> Task
            this.clientWithBaseUrl.GetAsync("/status/205") :> Task
        |] |> Task.WaitAll




