module HCup.GCTimer

open System
open System.Net.Http
open HCup.MethodCounter

let outstandingRequestCount = ref 0
let mutable lastRequestCount = 0
let mutable GCRun = false
// let client = new HttpClient()

let syncTimer = new System.Timers.Timer(500.0)
syncTimer.Elapsed.Add(fun arg ->
    if (lastRequestCount > 10 && lastRequestCount = outstandingRequestCount.Value)
    then
        if not GCRun
        then
            Console.WriteLine("Running GC {0} {1} accf:{2} accgr:{3} accr:{4} accs:{5} gvs:{6}",
                lastRequestCount,
                DateTime.Now.ToString("HH:mm:ss.ffff"),
                accountFilterCount.Value,
                accountsGroupCount.Value,
                accountsRecommendCount.Value,
                accountsSuggestCount.Value,
                getVisitsCount.Value)
            GCRun <- true
            GC.Collect(1)
        // client.GetAsync("http://127.0.0.1/visits/8").Result |> ignore
    else
        GCRun <- false
    lastRequestCount <- outstandingRequestCount.Value
)
syncTimer.AutoReset <- true
syncTimer.Enabled <- true
syncTimer.Start()