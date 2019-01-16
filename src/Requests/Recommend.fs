module Recommend

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

let inline intersectTwoArraysCount (first: byte[]) (second: byte[]) =
    let mutable count = 0
    let mutable i = 0
    let mutable j = 0
    while i < first.Length && j < second.Length do        
        if first.[i] = second.[j]
        then
            count <- count + 1
            j <- j + 1
            i <- i + 1
        else if first.[i] < second.[j]
        then
            i <- i + 1
        else
            j <- j + 1        
    count

let getCompatibility (target: Account) (acc: Account)  =
    let commonInterestsCount =
        if acc.interests |> isNull || target.interests |> isNull
        then 0
        else intersectTwoArraysCount acc.interests target.interests
    if commonInterestsCount = 0
    then
        None
    else
        let status = acc.status
        let statusrank = (status >>> 1) + ((status &&& 1uy) ^^^ 1uy) // 2 -> 2 , 1 -> 0, 0 -> 1
        let yearsDifference = 100 - (Math.Abs (acc.birth - target.birth))
        Some (acc.premiumNow, statusrank, commonInterestsCount, yearsDifference, -acc.id)
       

let cityFemaleOrder = [| bestFemaleUsersCity; bestFemaleUsers2City; bestFemaleUsers3City; bestSimpleFemaleUsersCity; bestSimpleFemaleUsers2City; bestSimpleFemaleUsers3City |]
let countryFemaleOrder = [| bestFemaleUsersCountry; bestFemaleUsers2Country; bestFemaleUsers3Country; bestSimpleFemaleUsersCountry; bestSimpleFemaleUsers2Country; bestSimpleFemaleUsers3Country |]
let cityMaleOrder = [| bestMaleUsersCity; bestMaleUsers2City; bestMaleUsers3City; bestSimpleMaleUsersCity; bestSimpleMaleUsers2City; bestSimpleMaleUsers3City |]
let countryMaleOrder = [| bestMaleUsersCountry; bestMaleUsers2Country; bestMaleUsers3Country; bestSimpleMaleUsersCountry; bestSimpleMaleUsers2Country; bestSimpleMaleUsers3Country |]

let emptyArray = ResizeArray<Account>()
let getDictionaryValue (dict: Dictionary<'K, ResizeArray<Account>>) (key: 'K) =
    if dict.ContainsKey(key)
    then dict.[key]
    else emptyArray

let getRecommendUsers sex city country =
    if city |> isNotNull
    then
        let mutable cityId = 0s
        if citiesWeightDictionary.TryGetValue(city, &cityId)
        then
            if sex = Common.male
            then
                seq {
                    for dict in cityFemaleOrder do    
                       yield! getDictionaryValue dict cityId
                }
            else
                seq {
                    for dict in cityMaleOrder do    
                       yield! getDictionaryValue dict cityId
            }
        else 
            Seq.empty
    else if country |> isNotNull
    then
        let mutable countryId = 0uy
        if countriesWeightDictionary.TryGetValue(country, &countryId)
        then
            if sex = Common.male
            then
                seq {
                    for dict in countryFemaleOrder do    
                       yield! getDictionaryValue dict countryId
                }
            else
                seq {
                    for dict in countryMaleOrder do    
                       yield! getDictionaryValue dict countryId
                }
        else
            Seq.empty
    else
        if sex = Common.male
        then            
            seq {
                for dict in countryFemaleOrder do 
                   for kv in dict do 
                       yield! kv.Value
            }
        else
            seq {
                for dict in countryMaleOrder do 
                   for kv in dict do 
                       yield! kv.Value
            }

let recommendationFields = [| "status_eq"; "fname_eq"; "sname_eq"; "birth_year"; "premium_now" |]
let getRecommendedAccounts (id, next, ctx : HttpContext) =
    Interlocked.Increment(accountsRecommendCount) |> ignore
    try
        if (id > accountsNumber)
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
                |> dict
            let limit = Int32.Parse(ctx.Request.Query.["limit"].[0])
            if limit <= 0 || limit > 20
            then
                setStatusCode 400 next ctx
            else
                let mutable city = null
                let mutable country = null
                keys.TryGetValue("city", &city) |> ignore
                keys.TryGetValue("country", &country) |> ignore
                let accounts =
                    getRecommendUsers target.sex city country
                let accs =
                    accounts
                    |> Seq.choose (fun acc -> getCompatibility target acc |> Option.map (fun comp -> (acc, comp)))
                    |> Seq.sortByDescending (fun (acc, comp) -> comp)
                    |> Seq.map (fun (acc, comp) -> acc)
                    |> Seq.truncate limit
                let memoryStream = serializeAccounts (accs, recommendationFields)
                writeResponse memoryStream next ctx
    with
    | :? KeyNotFoundException -> setStatusCode 400 next ctx
    | :? FormatException -> setStatusCode 400 next ctx
    | :? NotSupportedException as ex ->
        Console.WriteLine("NotSupportedException:" + ex.Message + " " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx
