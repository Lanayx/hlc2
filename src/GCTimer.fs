module HCup.GCTimer

open System
open System.Net.Http
open HCup.MethodCounter

let outstandingRequestCount = ref 0
let mutable lastRequestCount = 0
let mutable GCRun = false
let mutable rebuildingIndex = false


let syncTimer = new System.Timers.Timer(500.0)
// let client = new HttpClient()

let runTimer indexRebuild =
    syncTimer.Elapsed.Add(fun arg ->
        if (lastRequestCount > 10 && lastRequestCount = outstandingRequestCount.Value)
        then
            if shouldRebuildIndex
            then
                shouldRebuildIndex <- false
                rebuildingIndex <- true
                Console.WriteLine("Rebuilding index {0}",DateTime.Now.ToString("HH:mm:ss.ffff"))
                indexRebuild()
                Console.WriteLine("Rebuilding index finished {0}",DateTime.Now.ToString("HH:mm:ss.ffff"))
                rebuildingIndex <- false
                GCRun <- false

            if not GCRun && not rebuildingIndex
            then
                Console.WriteLine("Running GC {0} {1} Counts: accf:{2} accgr:{3} accr:{4} accs:{5} newacc:{6} updacc:{7} addl: {8}",
                    lastRequestCount,
                    DateTime.Now.ToString("HH:mm:ss.ffff"),
                    accountFilterCount.Value,
                    accountsGroupCount.Value,
                    accountsRecommendCount.Value,
                    accountsSuggestCount.Value,
                    newAccountCount.Value,
                    updateAccountCount.Value,
                    addLikesCount.Value)
                GCRun <- true
                GC.Collect(2, GCCollectionMode.Forced, true, true)
                Console.WriteLine("Finished GC {0} {1} Times: accf:{2} accgr:{3} accr:{4} accs:{5} newacc:{6} updacc:{7} addl: {8}",
                    lastRequestCount,
                    DateTime.Now.ToString("HH:mm:ss.ffff"),
                    accountFilterTime.Value,
                    accountsGroupTime.Value,
                    accountsRecommendTime.Value,
                    accountsSuggestTime.Value,
                    newAccountTime.Value,
                    updateAccountTime.Value,
                    addLikesTime.Value)
            // client.GetAsync("http://127.0.0.1/visits/8").Result |> ignore

        else
            GCRun <- false
        lastRequestCount <- outstandingRequestCount.Value
    )
    syncTimer.AutoReset <- true
    syncTimer.Enabled <- true
    syncTimer.Start()