module HCup.Requests.Suggest

open HCup.Dictionaries
open HCup
open HCup.Common
open Microsoft.AspNetCore.Http
open BitmapIndex
open System
open System.Collections.Generic
open System.Threading
open HCup.MethodCounter
open HCup.Filters
open HCup.BufferSerializers
open HCup.Requests.Filter
open Giraffe
open System.IO
open HCup.Models
open HCup.Requests

let inline getTsCount sumOfTs =
    single (Math.Round(float (sumOfTs / 1480000000.0f)))

let getSimilarityNew targetId (targetLike: SmartLike) (likers: ResizeArray<int>) (results:Dictionary<int,single>) =
    let targetTs = targetLike.sumOfTs / getTsCount targetLike.sumOfTs
    for liker in likers do
        if liker <> targetId
        then
            let smartLike = accounts.[liker].likes.[findLikeIndex accounts.[liker].likes targetLike.likee]
            let likerTs = smartLike.sumOfTs / getTsCount smartLike.sumOfTs
            let currrentSimilarity = 1.0f / Math.Abs(targetTs - likerTs)
            if (results.ContainsKey(liker))
            then results.[liker] <- results.[liker] + currrentSimilarity
            else results.[liker] <- currrentSimilarity

let suggestionFields = [| "status_eq"; "fname_eq"; "sname_eq"; |]
let getSuggestedAccounts (id, next, ctx : HttpContext) =
    Interlocked.Increment(accountsSuggestCount) |> ignore
    try
    if (id > accountsNumber || (box accounts.[id] |> isNull) )
    then
        setStatusCode 404 next ctx
    else
        let target = accounts.[id]
        let keys =
            ctx.Request.Query.Keys
            |> Seq.filter(fun key -> (key =~ "limit" || key =~ "query_id") |> not )
            |> Seq.sortBy (fun key -> groupFiltersOrder.[key])
            |> Seq.map (fun key ->
                    let value = ctx.Request.Query.[key].[0]
                    if String.IsNullOrEmpty(value)
                    then raise (KeyNotFoundException("Unknown value of get parameter"))
                    (key, value)
                )
            |> Seq.toArray
        let limit = Int32.Parse(ctx.Request.Query.["limit"].[0])
        if limit <= 0 || limit > 20
        then
            setStatusCode 400 next ctx
        else
            if target.likes |> isNull
            then
                let memoryStream = serializeAccounts ([||], Recommend.recommendationFields)
                writeResponse memoryStream next ctx
            else
                let filters =
                    keys
                    |> Seq.map (fun (key, value) -> suggestFilters.[key] value)
                let similaritiesWithUsers = Dictionary<int, single>()
                for targetLike in target.likes do
                    getSimilarityNew target.id targetLike (likesIndex.[targetLike.likee]) similaritiesWithUsers
                let similarAccounts =
                    similaritiesWithUsers.Keys
                    |> Seq.map (fun id -> accounts.[id])
                    |> Seq.filter(fun acc -> acc.sex = target.sex)
                let accs =
                    filters
                    |> Seq.fold (fun acc f -> acc |> Seq.filter f) similarAccounts
                    |> Seq.sortByDescending (fun acc -> similaritiesWithUsers.[acc.id])
                    |> Seq.collect(fun acc -> acc.likes)
                    |> Seq.filter (fun like -> findLikeIndex2 target.likes like < 0)
                    |> Seq.map (fun like -> accounts.[like.likee])
                    |> Seq.truncate limit
                let memoryStream = serializeAccounts (accs, suggestionFields)
                writeResponse memoryStream next ctx
    with
    | :? KeyNotFoundException -> setStatusCode 400 next ctx
    | :? FormatException -> setStatusCode 400 next ctx
    | :? NotSupportedException as ex ->
        Console.WriteLine("NotSupportedException:" + ex.Message + " " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx
