namespace Benchmarks

open System.Net.Http
open System
open BenchmarkDotNet.Attributes
open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Threading.Tasks

[<MemoryDiagnoser>]
type HttpClientBenchmarks() =
    member val simpleHttpClient = new HttpClient()
    member val clientWithBaseUrl = new HttpClient(BaseAddress = new Uri("http://192.168.0.101:30224"))


    [<Benchmark>]
    member this.SimpleHttpClient() =
        task {
            let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/get")
            let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/ip")
            let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/user-agent")
            let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/headers")
            let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/200")
            let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/201")
            let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/202")
            let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/203")
            let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/204")
            let! message = this.simpleHttpClient.GetAsync("http://192.168.0.101:30224/status/205")
            return message
        }



    [<Benchmark>]
    member this.BaseHttpClient() =
        task {
            let! message = this.clientWithBaseUrl.GetAsync("/get")
            let! message = this.clientWithBaseUrl.GetAsync("/ip")
            let! message = this.clientWithBaseUrl.GetAsync("/user-agent")
            let! message = this.clientWithBaseUrl.GetAsync("/headers")
            let! message = this.clientWithBaseUrl.GetAsync("/status/200")
            let! message = this.clientWithBaseUrl.GetAsync("/status/201")
            let! message = this.clientWithBaseUrl.GetAsync("/status/202")
            let! message = this.clientWithBaseUrl.GetAsync("/status/203")
            let! message = this.clientWithBaseUrl.GetAsync("/status/204")
            let! message = this.clientWithBaseUrl.GetAsync("/status/205")
            return message
        }

   


