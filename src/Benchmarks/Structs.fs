namespace Benchmarks

open BenchmarkDotNet.Attributes
open System.IO
open System.Runtime.CompilerServices

    
[<Interface>]
type ILog =
     abstract Info: string -> unit

module Log =
    
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let info (env: #ILog) fmt = 
        env.Info fmt
        env.Info fmt
        env.Info fmt
        env.Info fmt
        env.Info fmt
        env.Info fmt
        env.Info fmt
    
[<Struct;NoEquality;NoComparison>]
type AppEnv =
    interface ILog with
        member this.Info msg = TextWriter.Null.WriteLine(msg)

[<MemoryDiagnoser>]
type EnvBenchmark() =
    
    static let message = "hello world"
    
    [<Benchmark(Baseline=true)>]
    member _.ConsoleWriteLine() = 
        TextWriter.Null.WriteLine(message)
        TextWriter.Null.WriteLine(message)
        TextWriter.Null.WriteLine(message)
        TextWriter.Null.WriteLine(message)
        TextWriter.Null.WriteLine(message)
        TextWriter.Null.WriteLine(message)
        TextWriter.Null.WriteLine(message)

    [<Benchmark>]
    member _.LogInfo() = Log.info (AppEnv()) message
