module HCup.App

open System
open System.Buffers
open System.IO
open System.IO.Compression
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks
open System.Text
open System.Linq
open System.Threading
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open HCup.Models
open HCup.RequestCounter
open HCup.Actors
open HCup.Parser
open HCup.Helpers
open HCup.BufferSerializers
open HCup.MethodCounter
open HCup.Dictionaries
open Giraffe
open FSharp.Control.Tasks.V2.ContextInsensitive
open Microsoft.AspNetCore.Server.Kestrel.Core
open Microsoft.AspNetCore.Server.Kestrel.Transport
open Utf8Json
open System
open Filters
open Microsoft.Extensions.DependencyInjection
open System.Text.RegularExpressions
open System.Diagnostics
open BitmapIndex
open System.Collections.ObjectModel

// ---------------------------------
// Web app
// ---------------------------------

type IntReverseComparer() =
    interface IComparer<int> with
        member this.Compare(x,y) =
            if x > y
            then -1
            else
                if x < y
                then 1
                else 0
let intReverseComparer = new IntReverseComparer()

let accounts = Array.zeroCreate(1400000)
let mutable accountsNumber = 0
let mutable interestsIndex = BitmapIndex()

let inline getRevAccounts() =
    seq {
        for i = accountsNumber downto 1 do
            yield accounts.[i]
    }

let inline getAccounts() =
    accounts
    |> Seq.skip 1
    |> Seq.take accountsNumber

let jsonStringValues = StringValues "application/json"

let inline deserializeObjectUnsafe<'a> (str: string) =
    JsonSerializer.Deserialize<'a>(str)

let inline deserializeObject<'a> (str: string) =
    try
        Some <| JsonSerializer.Deserialize<'a>(str)
    with
    | exn ->
        None


type LikesComparer() =
    interface IEqualityComparer<Like> with
        member this.Equals(x,y) = x.id = y.id
        member this.GetHashCode(obj) = obj.id
let likesComparer = new LikesComparer()

let inline writeResponse (response : MemoryStream) (next : HttpFunc) (ctx: HttpContext) =
    let length = response.Position
    ctx.Response.Headers.["Content-Type"] <- jsonStringValues
    ctx.Response.Headers.ContentLength <- Nullable(length)
    let bytes = response.GetBuffer()
    task {
        do! ctx.Response.Body.WriteAsync(bytes, 0, (int32)length)
        do! ctx.Response.Body.FlushAsync()
        ArrayPool.Shared.Return bytes
        return! next ctx
    }

let postResponseBytes = utf8 "{}"
let inline writePostResponse (code: int) (next : HttpFunc) (ctx: HttpContext) =
    let length = postResponseBytes.Length
    ctx.Response.Headers.["Content-Type"] <- jsonStringValues
    ctx.Response.Headers.ContentLength <- Nullable(int64 length)
    ctx.Response.StatusCode <- code
    task {
        do! ctx.Response.Body.WriteAsync(postResponseBytes, 0, length)
        do! ctx.Response.Body.FlushAsync()
        return! next ctx
    }

let getStringWeight (str: string) =
    let strChars = str |> Seq.truncate 11
    let mutable multiplier = 0L
    let mutable divisor = 0L
    if (int)str.[0] > 200
    then
        multiplier <- 36028797018963968L //32^11
        divisor <- 32L
    else
        multiplier <- 1521681143169024L // 24^11
        divisor <- 24L
    let mutable result = 0L
    for chr in strChars do
        let intChr = int chr
        let diff =
            match intChr with
            | smallRussianLetter when smallRussianLetter >= 1072 -> intChr - 1070 + 10
            | bigRussianLetter when bigRussianLetter >= 1040 -> intChr - 1040 + 10
            | smallEnglishLetter when smallEnglishLetter >= 97 -> intChr - 97 + 20 // hack to compare Pitbull with PS3
            | bigEnglishLetter when bigEnglishLetter >= 65 -> intChr - 65 + 10
            | number when number >= 48 -> intChr - 47
            | _ -> intChr - 32
        result <- result + (int64 diff) * multiplier
        multiplier <- multiplier / divisor
    result

let inline handleInterests (interests: string[]) (account: Account) =
    account.interests <-
        interests
        |> Array.map(fun interest ->
                let mutable weight = 0L
                if interestsWeightDictionary.TryGetValue(interest, &weight)
                then
                    weight
                else
                    weight <- getStringWeight interest
                    interestsWeightDictionary.Add(interest, weight)
                    interestsSerializeDictionary.Add(weight, utf8 interest)
                    weight
            )

let updateCityIndex oldSex oldStatus oldCity (account: Account) (deletePrevious: bool) = 
    if deletePrevious
    then
        citySexGroups.[oldSex].[oldCity] <- citySexGroups.[oldSex].[oldCity] - 1
        cityStatusGroups.[oldStatus].[oldCity] <- cityStatusGroups.[oldStatus].[oldCity] - 1

    let mutable sexCount: int = 0    
    if citySexGroups.[account.sex].TryGetValue(account.city, &sexCount)
    then
        citySexGroups.[account.sex].[account.city] <- sexCount + 1
    else
        citySexGroups.[account.sex].[account.city] <- 1

    let mutable statusCount: int = 0
    if cityStatusGroups.[account.status].TryGetValue(account.city, &statusCount)
    then
        cityStatusGroups.[account.status].[account.city] <- statusCount + 1
    else
        cityStatusGroups.[account.status].[account.city] <- 1

let updateCountryIndex oldSex oldStatus oldCountry (account: Account) (deletePrevious: bool) =
    if deletePrevious
    then
        countrySexGroups.[oldSex].[oldCountry] <- countrySexGroups.[oldSex].[oldCountry] - 1
        countryStatusGroups.[oldStatus].[oldCountry] <- countryStatusGroups.[oldStatus].[oldCountry] - 1

    let mutable sexCount: int = 0    
    if countrySexGroups.[account.sex].TryGetValue(account.country, &sexCount)
    then
        countrySexGroups.[account.sex].[account.country] <- sexCount + 1
    else
        countrySexGroups.[account.sex].[account.country] <- 1

    let mutable statusCount: int = 0
    if countryStatusGroups.[account.status].TryGetValue(account.country, &statusCount)
    then
        countryStatusGroups.[account.status].[account.country] <- statusCount + 1
    else
        countryStatusGroups.[account.status].[account.country] <- 1

let handleCity city (account: Account) (deletePrevious: bool) =
    if deletePrevious && account.city > 0L
    then
        citiesIndex.[account.city].Remove(account.id) |> ignore
    let mutable weight = 0L
    if citiesWeightDictionary.TryGetValue(city, &weight)
    then
        account.city <- weight
    else
        weight <- getStringWeight city
        citiesWeightDictionary.Add(city, weight)
        citiesSerializeDictionary.Add(weight, utf8 city)
        account.city <- weight
    let mutable cityUsers: SortedSet<Int32> = null
    if citiesIndex.TryGetValue(account.city, &cityUsers)
    then
        cityUsers.Add(account.id) |> ignore
    else
        cityUsers <- SortedSet(intReverseComparer)
        cityUsers.Add(account.id) |> ignore
        citiesIndex.[account.city] <- cityUsers

let inline handleCountry country (account: Account) (deletePrevious: bool) =
    if deletePrevious && account.country > 0L
    then
        countriesIndex.[account.country].Remove(account.id) |> ignore
    let mutable weight = 0L
    if countriesWeightDictionary.TryGetValue(country, &weight)
    then
        account.country <- weight
    else
        weight <- getStringWeight country
        countriesWeightDictionary.Add(country, weight)
        countriesSerializeDictionary.Add(weight, utf8 country)
        account.country <- weight

    if account.country > 0L
    then
        let mutable countryUsers: SortedSet<Int32> = null
        if countriesIndex.TryGetValue(account.country, &countryUsers)
        then
            countryUsers.Add(account.id) |> ignore
        else
            countryUsers <- SortedSet(intReverseComparer)
            countryUsers.Add(account.id) |> ignore
            countriesIndex.[account.country] <- countryUsers

let inline handleFirstName (fname: string) (account: Account) =
    let mutable weight = 0L
    if namesWeightDictionary.TryGetValue(fname, &weight)
    then
        account.fname <- weight
    else
        weight <- getStringWeight fname
        namesWeightDictionary.Add(fname, weight)
        namesSerializeDictionary.Add(weight, utf8 fname)
        account.fname <- weight

let inline handleSecondName (sname: string) (account: Account) =
    let mutable weight = 0L
    if snamesWeightDictionary.TryGetValue(sname, &weight)
    then
        account.sname <- weight
    else
        weight <- getStringWeight sname
        snamesWeightDictionary.Add(sname, weight)
        snamesSerializeDictionary.Add(weight, struct(sname,utf8 sname))
        account.sname <- weight

let inline handleEmail (email: string) (account: Account) =
    let atIndex = email.IndexOf('@', StringComparison.Ordinal)
    let emailDomain = email.Substring(atIndex+1)
    if atIndex >= 0 && emailsDictionary.Contains(email) |> not
    then
        if account.email |> isNotNull
        then emailsDictionary.Remove(account.email) |> ignore
        account.email <- email
        account.emailDomain <- emailDomain
    else
        raise (ArgumentOutOfRangeException("Invalid email"))

let inline handlePhone (phone: string) (account: Account) =
    let phoneCode =
        if phone |> isNull
        then 0
        else Int32.Parse(phone.Substring(2,3))
    account.phone <- phone
    account.phoneCode <- phoneCode

let inline addLikeToDictionary liker likee likeTs =
    if likesIndex.ContainsKey(likee) |> not
    then likesIndex.Add(likee, SortedDictionary<int, struct(single*int)>(intReverseComparer))
    let likers = likesIndex.[likee]
    if likers.ContainsKey(liker)
    then
        let struct(ts, count) = likers.[liker]
        likers.[liker] <- struct(ts + (single)likeTs, count+1)
    else
        likers.[liker] <- struct((single)likeTs, 1)

let handleLikes (likes: Like[]) (account: Account) (deletePrevious: bool) =
    if deletePrevious
    then
        for likeId in account.likes do
            let likers = likesIndex.[likeId]
            likers.Remove(account.id) |> ignore
    for like in likes do
        addLikeToDictionary account.id like.id like.ts

    account.likes <-
        likes
        |> Array.map(fun like -> like.id)
        |> Array.distinct
        |> Array.sortDescending
        |> ResizeArray

let createAccount (accUpd: AccountUpd): Account =
    let account = Account()
    account.id <- accUpd.id.Value
    handleEmail accUpd.email account
    handlePhone accUpd.phone account
    if accUpd.fname |> isNotNull
    then
        handleFirstName accUpd.fname account
    if accUpd.sname |> isNotNull
    then
        handleSecondName accUpd.sname account
    if accUpd.interests |> isNotNull
    then
        handleInterests accUpd.interests account
    account.status <- getStatus accUpd.status
    if (box accUpd.premium) |> isNotNull
    then
        account.premiumStart <- accUpd.premium.start
        account.premiumFinish <- accUpd.premium.finish
    account.premiumNow <- box accUpd.premium |> isNotNull && accUpd.premium.start <= currentTs && accUpd.premium.finish > currentTs
    account.sex <- accUpd.sex.[0]
    account.birth <- accUpd.birth.Value
    account.birthYear <- (convertToDate accUpd.birth.Value).Year
    account.joined <- accUpd.joined.Value
    account.joinedYear <- (convertToDate accUpd.joined.Value).Year

    // handle fields with indexes to not fill indexes on failures
    if accUpd.likes |> isNotNull
    then
        handleLikes accUpd.likes account false
    if accUpd.city |> isNotNull
    then
        handleCity accUpd.city account false
    if accUpd.country |> isNotNull
    then
        handleCountry accUpd.country account false
    updateCityIndex ' ' 0 0L account false
    updateCountryIndex ' ' 0 0L account false
    emailsDictionary.Add(account.email) |> ignore
    account

let updateExistingAccount (existing: Account, accUpd: AccountUpd) =
    let oldCity = existing.city
    let oldCountry = existing.country
    let oldStatus = existing.status
    let oldSex = existing.sex

    if accUpd.birth.HasValue
    then
        existing.birth <- accUpd.birth.Value
        existing.birthYear <- (convertToDate accUpd.birth.Value).Year
    if accUpd.joined.HasValue
    then
        existing.joined <- accUpd.joined.Value
        existing.joinedYear <- (convertToDate accUpd.joined.Value).Year
    if accUpd.sex |> isNotNull
    then
        if accUpd.sex.Length > 1
        then raise (ArgumentOutOfRangeException("Sex is wrong"))
        existing.sex <- accUpd.sex.[0]
    if accUpd.email |> isNotNull
    then
        handleEmail accUpd.email existing
    if accUpd.phone |> isNotNull
    then
        handlePhone accUpd.phone existing
    if accUpd.fname |> isNotNull
    then
        handleFirstName accUpd.fname existing
    if accUpd.sname |> isNotNull
    then
        handleSecondName accUpd.sname existing
    if accUpd.interests |> isNotNull
    then
        handleInterests accUpd.interests existing
    if accUpd.status |> isNotNull
    then
        existing.status <- getStatus accUpd.status
    if box accUpd.premium |> isNotNull
    then
        existing.premiumStart <- accUpd.premium.start
        existing.premiumFinish <- accUpd.premium.finish
        existing.premiumNow <- accUpd.premium.start <= currentTs && accUpd.premium.finish > currentTs

    // handle fields with indexes to not fill indexes on failures
    if accUpd.likes |> isNotNull
    then
        handleLikes accUpd.likes existing true
    if accUpd.city |> isNotNull
    then
        handleCity accUpd.city existing true
    if accUpd.country |> isNotNull
    then
        handleCountry accUpd.country existing true 
    if accUpd.city |> isNotNull || accUpd.sex |> isNotNull || accUpd.status |> isNotNull
    then
        updateCityIndex oldSex oldStatus oldCity existing true
    if accUpd.country |> isNotNull || accUpd.sex |> isNotNull || accUpd.status |> isNotNull
    then
        updateCountryIndex oldSex oldStatus oldCountry existing true  
    if accUpd.email |> isNotNull
    then
        emailsDictionary.Add(accUpd.email) |> ignore
    ()

let getInterestContainsAccounts (str: string) =
    let mutable criteria = Unchecked.defaultof<BICriteria>
    for value in str.Split(',') do
        let interest = interestsWeightDictionary.[value]
        if criteria |> isNull
        then
            criteria <- BICriteria.equals(BIKey(0, interest))
        else
            criteria <- criteria.``and``(BICriteria.equals(BIKey(0, interest)))
    let result = interestsIndex.query(criteria).GetPositions()
    seq{
        for i = result.Count-1 downto 0 do
            yield accounts.[result.[i]]
    }

let getInterestAnyAccounts (str: string) =
    let mutable criteria = Unchecked.defaultof<BICriteria>
    for value in str.Split(',') do
        let interest = interestsWeightDictionary.[value]
        if criteria |> isNull
        then
            criteria <- BICriteria.equals(BIKey(0, interest))
        else
            criteria <- criteria.``or``(BICriteria.equals(BIKey(0, interest)))
    let result = interestsIndex.query(criteria).GetPositions()
    seq{
        for i = result.Count-1 downto 0 do
            yield accounts.[result.[i]]
    }

let inline getLikesContainsAccount value =
    let key = Int32.Parse(value)
    if likesIndex.ContainsKey(key)
    then
        seq likesIndex.[key].Keys
    else
        Seq.empty
    |> Seq.map (fun id -> accounts.[id])

let getLikesContainsAccounts (value: string) =
    let values = value.Split(',')
    if (value.Length <> 1)
    then
        let keys =
            values
            |> Seq.map (fun str -> Int32.Parse(str))
            |> Seq.cache
        if Seq.forall (fun key -> likesIndex.ContainsKey(key)) keys
        then
            Seq.collect (fun key -> likesIndex.[key].Keys) keys
        else
            Seq.empty
        |> Seq.countBy id
        |> Seq.filter (fun (id, count) -> count = values.Length)
        |> Seq.map (fun (id, _) -> accounts.[id])
    else
        getLikesContainsAccount value

let arrayMaxWithInd (arr: int[]) =
    let mutable max = 0
    let mutable maxIndex = 0
    for i in [0..arr.Length-1] do
        if arr.[i] > max
        then
            max <- arr.[i]
            maxIndex <- i
    struct(max, maxIndex)

let arrayMinInd (arr: KeyValuePair<int64,int>[]) =
    let mutable minIndex = 0
    for i in [0..arr.Length-1] do
        if arr.[i].Value < arr.[minIndex].Value 
            || (arr.[i].Value = arr.[minIndex].Value && arr.[i].Key < arr.[minIndex].Key)
        then
            minIndex <- i
    minIndex

let sortedCollectDict<'T> (sortedSets: KeyValuePair<'T,Dictionary<int64, int>> seq) =
    let setsArray = sortedSets |> Seq.toArray
    let enumerators = setsArray |> Array.map (fun kv -> kv.Value.GetEnumerator())
    let types = setsArray |> Array.map (fun kv -> kv.Key)
    for i in [0..enumerators.Length-1] do
        enumerators.[i].MoveNext() |> ignore
    let firstValues = enumerators |> Array.map (fun en -> en.Current)
    seq {
        let mutable enumerable = enumerators.Length
        while enumerable > 0 do
            let minIndex = firstValues |> arrayMinInd
            let t = types.[minIndex]
            let values = enumerators.[minIndex].Current
            if (enumerators.[minIndex].MoveNext())
            then
                firstValues.[minIndex] <- enumerators.[minIndex].Current
            else
                firstValues.[minIndex] <- KeyValuePair(Int64.MaxValue,Int32.MaxValue)
                enumerable <- enumerable - 1
            yield (values.Key, t), values.Value
    }
    |> Seq.cache

let sortedReverseCollect (sortedSets: SortedSet<int> seq) =
    let setsArray = sortedSets |> Seq.toArray
    let enumerators = setsArray |> Array.map (fun set -> set.GetEnumerator())
    for i in [0..enumerators.Length-1] do
        enumerators.[i].MoveNext() |> ignore
    let firstValues = enumerators |> Array.map (fun en -> en.Current)
    seq {
        let mutable enumerable = enumerators.Length
        while enumerable > 0 do
            let struct(max, maxIndex) = firstValues |> arrayMaxWithInd
            if (enumerators.[maxIndex].MoveNext())
            then
                firstValues.[maxIndex] <- enumerators.[maxIndex].Current
            else
                firstValues.[maxIndex] <- 0
                enumerable <- enumerable - 1
            yield max
    }
    |> Seq.cache

let getCityAnyAccounts (value: string) =
    let values = value.Split(',')
    if (value.Length <> 1)
    then
        values
        |> Seq.filter (fun key -> citiesWeightDictionary.ContainsKey(key))
        |> Seq.map (fun key -> citiesWeightDictionary.[key])
        |> Seq.map (fun weight -> citiesIndex.[weight])
        |> sortedReverseCollect
        |> Seq.map (fun id -> accounts.[id])
    else
        if citiesWeightDictionary.ContainsKey(value) |> not
        then
            Seq.empty
        else
            let weight = citiesWeightDictionary.[value]
            citiesIndex.[weight]
            |> Seq.map (fun id -> accounts.[id])

let getCityEqAccounts (value: string) =
    let mutable weight = 0L
    if citiesWeightDictionary.TryGetValue(value, &weight)
    then
        citiesIndex.[weight]
        |> Seq.map (fun id -> accounts.[id])
    else
        Seq.empty

let getCountryEqAccounts (value: string) =
    let mutable weight = 0L
    if countriesWeightDictionary.TryGetValue(value, &weight)
    then
        countriesIndex.[weight]
        |> Seq.map (fun id -> accounts.[id])
    else
        Seq.empty

let rec getFilteredAccountsByQuery keys (ctx : HttpContext) =
    match keys with
    | [] -> getRevAccounts()
    | h::t when h = "likes_contains" -> getLikesContainsAccounts (ctx.Request.Query.["likes_contains"].[0])
    | h::t when h = "interests_contains" -> getInterestContainsAccounts (ctx.Request.Query.["interests_contains"].[0])
    | h::t when h = "interests_any" -> getInterestAnyAccounts (ctx.Request.Query.["interests_any"].[0])
    | h::t when h = "city_any" -> getCityAnyAccounts (ctx.Request.Query.["city_any"].[0])
    | h::t when h = "city_eq" -> getCityEqAccounts (ctx.Request.Query.["city_eq"].[0])
    | h::t when h = "country_eq" -> getCountryEqAccounts (ctx.Request.Query.["country_eq"].[0])
    | h::t -> getFilteredAccountsByQuery t ctx

let getFilteredAccounts (next, ctx : HttpContext) =
    Interlocked.Increment(accountFilterCount) |> ignore
    try
        let keys =
            ctx.Request.Query.Keys
            |> Seq.filter(fun key -> (key =~ "limit" || key =~ "query_id") |> not )
            |> Seq.toList
        let filters =
            keys
            |> Seq.sortBy (fun key -> filtersOrder.[key])
            |> Seq.map (fun key -> filters.[key] ctx.Request.Query.[key].[0])
        let accounts =
            getFilteredAccountsByQuery keys ctx
        let accs =
            filters
            |> Seq.fold (fun acc f -> acc |> Seq.filter f) accounts
            |> Seq.truncate (Int32.Parse(ctx.Request.Query.["limit"].[0]))
        let memoryStream = serializeAccounts (accs, keys)
        writeResponse memoryStream next ctx
    with
    | :? IndexOutOfRangeException ->
        setStatusCode 400 next ctx
    | :? KeyNotFoundException ->
        setStatusCode 400 next ctx
    | :? FormatException ->
        setStatusCode 400 next ctx
    | :? NotSupportedException ->
        Console.WriteLine("NotSupportedException: " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx

let seq_sort order =
    if order = -1
    then Seq.sortByDescending
    else Seq.sortBy

let seq_take order length take =
    if order = -1
    then 
        Seq.skip (length - take) >> Seq.rev
    else 
        Seq.truncate take

let applyGrouping (memoryStream: byref<MemoryStream>, groupKey, order, accs: Account seq, limit) =
    match groupKey with
    | "sex" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.sex)
            |> Seq.map (fun (key, group) -> (key, group |> Seq.length))
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsSex(groups, "sex")
    | "status" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.status)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsStatus(groups, "status")
    | "country" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.country)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsCountry(groups, "country")
    | "city" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.city)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsCity(groups, "city")
    | "interests" ->
        let interests =
            accs
            |> Seq.filter (fun acc -> acc.interests |> isNotNull)
            |> Seq.collect (fun acc -> acc.interests)
            |> Seq.groupBy id
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsInterests(interests , "interests")
    | "city,status" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.city, acc.status)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Status(groups, "city", "status")
    | "city,sex" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.city, acc.sex)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Sex(groups, "city", "sex")
    | "country,sex" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.country, acc.sex)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Sex(groups, "country", "sex")
    | "country,status" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.country, acc.status)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Status(groups, "country", "status")
    | _ ->
        ()

let getGroupsWithEmptyFilter (memoryStream: byref<MemoryStream>, groupKey, order, limit) =
    let accs = getAccounts()
    match groupKey with
    | "sex" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.sex)
            |> Seq.map (fun (key, group) -> (key, group |> Seq.length))
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsSex(groups, "sex")
    | "status" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.status)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsStatus(groups, "status")
    | "country" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.country)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsCountry(groups, "country")
    | "city" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.city)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsCity(groups, "city")
    | "interests" ->
        let interests =
            accs
            |> Seq.filter (fun acc -> acc.interests |> isNotNull)
            |> Seq.collect (fun acc -> acc.interests)
            |> Seq.groupBy id
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsInterests(interests , "interests")
    | "city,status" ->
        let groups =
            cityStatusGroups
            |> sortedCollectDict
            |> seq_take order (cityStatusGroups.[0].Count + cityStatusGroups.[1].Count + cityStatusGroups.[2].Count) limit
        memoryStream <- serializeGroups2Status(groups, "city", "status")
    | "city,sex" ->
        let groups =
            citySexGroups
            |> sortedCollectDict
            |> seq_take order (citySexGroups.['f'].Count + citySexGroups.['m'].Count) limit
        memoryStream <- serializeGroups2Sex(groups, "city", "sex")
    | "country,sex" ->
        let groups =
            countrySexGroups
            |> sortedCollectDict
            |> seq_take order (countrySexGroups.['f'].Count + countrySexGroups.['m'].Count) limit
        memoryStream <- serializeGroups2Sex(groups, "country", "sex")
    | "country,status" ->
        let groups =
            countryStatusGroups
            |> sortedCollectDict
            |> seq_take order (countryStatusGroups.[0].Count + countryStatusGroups.[1].Count + countryStatusGroups.[2].Count) limit
        memoryStream <- serializeGroups2Status(groups, "country", "status")
    | _ ->
        ()

let inline intersectTwoArraysCount (first: int64[]) (second: int64[]) =
    let mutable count = 0
    let mutable i = 0
    let mutable j = 0
    while i < first.Length do
        j <- 0
        while j < second.Length do
            if first.[i] = second.[j]
            then
                count <- count + 1
                j <- second.Length
            else
                j <- j + 1
        i <- i + 1
    count

let rec getGroupedAccountsByQuery keys (ctx : HttpContext) =
    match keys with
    | [] -> getAccounts()
    | h::t when h = "likes" -> getLikesContainsAccount (ctx.Request.Query.["likes"].[0])
    | h::t -> getGroupedAccountsByQuery t ctx

let getGroupedAccounts (next, ctx : HttpContext) =
    Interlocked.Increment(accountsGroupCount) |> ignore
    try
        let keys =
            ctx.Request.Query.Keys
            |> Seq.filter(fun key -> (key =~ "limit" || key =~ "query_id" || key =~ "order" || key=~"keys") |> not )
            |> Seq.toList
        let limit = Int32.Parse(ctx.Request.Query.["limit"].[0])
        let groupKey =
            ctx.Request.Query.["keys"].[0]
        let order =
                Int32.Parse(ctx.Request.Query.["order"].[0])
        let mutable memoryStream: MemoryStream = null
        if keys.IsEmpty
        then
            getGroupsWithEmptyFilter(&memoryStream, groupKey, order, limit)
        else
            let filters =
                keys
                |> Seq.sortBy (fun key -> groupFiltersOrder.[key])
                |> Seq.map (fun key -> groupFilters.[key] ctx.Request.Query.[key].[0])
            let accounts =
                getGroupedAccountsByQuery keys ctx
            let accs =
                filters
                |> Seq.fold (fun acc f -> acc |> Seq.filter f) accounts
            applyGrouping(&memoryStream, groupKey, order, accs, limit)
        if (memoryStream |> isNotNull)
        then writeResponse memoryStream next ctx
        else setStatusCode 400 next ctx
    with
    | :? KeyNotFoundException -> setStatusCode 400 next ctx
    | :? FormatException -> setStatusCode 400 next ctx
    | :? NotSupportedException as ex ->
        Console.WriteLine("NotSupportedException:" + ex.Message + " " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx

let getCompatibility (target: Account) (acc: Account)  =
    let commonInterestsCount =
        if acc.interests |> isNull || target.interests |> isNull
        then 0
        else intersectTwoArraysCount acc.interests target.interests
    if commonInterestsCount = 0
    then
        None
    else
        let statusrank =
            match acc.status with
            | Helpers.freeStatus -> 2
            | Helpers.complexStatus -> 1
            | Helpers.occupiedStatus -> 0
            | _ -> failwith "Invalid status"
        let yearsDifference = 100 - (Math.Abs (acc.birth - target.birth))
        Some (acc.premiumNow, statusrank, commonInterestsCount, yearsDifference, -acc.id)

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
                |> Seq.toArray
            let limit = Int32.Parse(ctx.Request.Query.["limit"].[0])
            if limit <= 0 || limit > 20
            then
                setStatusCode 400 next ctx
            else
                let filters =
                    keys
                    |> Seq.map (fun (key, value) -> recommendFilters.[key] value)
                let accounts =
                    getAccounts()
                    |> Seq.filter(fun acc -> acc.sex <> target.sex)
                let accs =
                    filters
                    |> Seq.fold (fun acc f -> acc |> Seq.filter f) accounts
                    |> Seq.map (fun acc -> acc, getCompatibility target acc)
                    |> Seq.filter (fun (acc, compat) -> compat.IsSome)
                    |> Seq.sortByDescending (fun (acc, comp) -> comp.Value)
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

let getSimilarityNew targetId (likers: SortedDictionary<int,struct(single*int)>) (results:Dictionary<int,single>) =
    let struct(targTsSum, count) = likers.[targetId]
    let targTs = targTsSum / (single) count
    for liker in likers do
        if liker.Key <> targetId
        then
            let struct(likerTsSum, count) = liker.Value
            let likerTs = likerTsSum / (single) count
            let currrentSimilarity = 1.0f / Math.Abs(targTs - likerTs)
            if (results.ContainsKey(liker.Key))
            then results.[liker.Key] <- results.[liker.Key] + currrentSimilarity
            else results.[liker.Key] <- currrentSimilarity

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
                let memoryStream = serializeAccounts ([||], recommendationFields)
                writeResponse memoryStream next ctx
            else
                let filters =
                    keys
                    |> Seq.map (fun (key, value) -> recommendFilters.[key] value)
                let similaritiesWithUsers = Dictionary<int, single>()
                for likeId in target.likes do
                    getSimilarityNew target.id (likesIndex.[likeId]) similaritiesWithUsers
                let similarAccounts =
                    similaritiesWithUsers.Keys
                    |> Seq.map (fun id -> accounts.[id])
                    |> Seq.filter(fun acc -> acc.sex = target.sex)
                let accs =
                    filters
                    |> Seq.fold (fun acc f -> acc |> Seq.filter f) similarAccounts
                    |> Seq.sortByDescending (fun acc -> similaritiesWithUsers.[acc.id])
                    |> Seq.collect(fun acc -> acc.likes)
                    |> Seq.filter (fun likeId -> target.likes.Contains(likeId) |> not)
                    |> Seq.map (fun likeId -> accounts.[likeId])
                    |> Seq.truncate limit
                let memoryStream = serializeAccounts (accs, suggestionFields)
                writeResponse memoryStream next ctx
    with
    | :? KeyNotFoundException -> setStatusCode 400 next ctx
    | :? FormatException -> setStatusCode 400 next ctx
    | :? NotSupportedException as ex ->
        Console.WriteLine("NotSupportedException:" + ex.Message + " " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx

let findUser (next, ctx : HttpContext) =
    let likeIds = ctx.Request.Query.["likes"].[0].Split(',') |> Array.map Int32.Parse
    let users =
        accounts
        |> Seq.filter(fun acc -> (box acc) |> isNotNull)
        |> Seq.filter(fun acc -> acc.likes |> isNotNull)
        |> Seq.filter(fun acc -> acc.likes.Intersect(likeIds).Count() >= likeIds.Length)
    json users next ctx

let private accountsFilterString = "/accounts/filter/"
let private accountsGroupString = "/accounts/group/"
let private findUserString = "/findUser/"

let customGetRoutef : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        match ctx.Request.Path.Value with
        | filterPath when filterPath =~ accountsFilterString ->
             getFilteredAccounts (next, ctx)
        | filterPath when filterPath =~ accountsGroupString ->
             getGroupedAccounts (next, ctx)
        | filterPath when filterPath =~ findUserString ->
             findUser (next, ctx)
        | filterPath ->
            if filterPath.Length > 10
            then
                let sp = filterPath.AsSpan()
                let mainPart = sp.Slice(10)
                let indexOfSlash = mainPart.IndexOf('/')
                if indexOfSlash > 0
                then
                    let stringId = mainPart.Slice(0,indexOfSlash)
                    let ending = mainPart.Slice(indexOfSlash + 1)
                    let mutable id = 0
                    if Int32.TryParse(stringId, &id)
                    then
                        if "recommend/" == ending
                        then
                            getRecommendedAccounts (id, next, ctx)
                        else
                            if "suggest/" == ending
                            then
                                getSuggestedAccounts (id, next, ctx)
                            else
                                setStatusCode 404 next ctx
                    else
                        setStatusCode 404 next ctx
                else
                    setStatusCode 404 next ctx
            else
                setStatusCode 404 next ctx

let private newUserString = "/accounts/new/"
let private newLikesString = "/accounts/likes/"

let newAccount (next, ctx : HttpContext) =
    Interlocked.Increment(newAccountCount) |> ignore
    task {
        try
            let! json = ctx.ReadBodyFromRequestAsync()
            let account = deserializeObjectUnsafe<AccountUpd>(json)
            if account.id.HasValue |> not
            then
                return! setStatusCode 400 next ctx
            else
                accounts.[account.id.Value] <- createAccount account
                Interlocked.Increment(&accountsNumber) |> ignore
                return! writePostResponse 201 next ctx
        with
        | :? ArgumentOutOfRangeException ->
            return! setStatusCode 400 next ctx
        | :? JsonParsingException ->
            return! setStatusCode 400 next ctx
    }

let updateAccount (id, next, ctx : HttpContext) =
    Interlocked.Increment(updateAccountCount) |> ignore
    if (box accounts.[id] |> isNull)
    then
        setStatusCode 404 next ctx
    else
        task {
            try
                let! json = ctx.ReadBodyFromRequestAsync()
                let account = deserializeObjectUnsafe<AccountUpd>(json)
                let target = accounts.[id]
                let rollback = target.CreateCopy()
                try
                    updateExistingAccount(target, account)
                    return! writePostResponse 202 next ctx
                with
                | :? ArgumentOutOfRangeException ->
                    accounts.[id] <- rollback
                    return! setStatusCode 400 next ctx

                | :? KeyNotFoundException ->
                    return! setStatusCode 400 next ctx
            with
            | :? JsonParsingException ->
                return! setStatusCode 400 next ctx
        }

let addLikes (next, ctx : HttpContext) =
    Interlocked.Increment(addLikesCount) |> ignore
    task {
        try
            let! json = ctx.ReadBodyFromRequestAsync()
            let likes = deserializeObjectUnsafe<LikesUpd>(json)
            let wrongLike =
                likes.likes
                |> Array.tryFind(fun like ->
                    box accounts.[like.liker] |> isNull || box accounts.[like.likee] |> isNull)
            match wrongLike with
            | Some _ -> return! setStatusCode 400 next ctx
            | None ->
                for like in likes.likes do
                    let acc = accounts.[like.liker]
                    if (acc.likes |> isNull)
                    then acc.likes <- ResizeArray(seq { yield like.likee })
                    else
                        if (acc.likes.Contains(like.likee) |> not)
                        then
                            acc.likes.Add(like.likee)
                            acc.likes.Sort(intReverseComparer)
                    addLikeToDictionary like.liker like.likee like.ts
                return! writePostResponse 202 next ctx
        with
        | :? JsonParsingException ->
            return! setStatusCode 400 next ctx
    }

let customPostRoutef : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        shouldRebuildIndex <- true
        match ctx.Request.Path.Value with
        | filterPath when filterPath =~ newUserString ->
             newAccount (next, ctx)
        | filterPath when filterPath =~ newLikesString ->
             addLikes (next, ctx)
        | filterPath ->
            if filterPath.Length > 10
            then
                let sp = filterPath.AsSpan()
                let mainPart = sp.Slice(10)
                let indexOfSlash = mainPart.IndexOf('/')
                if indexOfSlash > 0
                then
                    let stringId = mainPart.Slice(0,indexOfSlash)
                    let mutable id = 0
                    if Int32.TryParse(stringId, &id)
                    then
                        updateAccount (id, next, ctx)
                    else
                        setStatusCode 404 next ctx
                else
                    setStatusCode 404 next ctx
            else
                setStatusCode 404 next ctx


let buildBitMapIndex() =
    interestsIndex <- BitmapIndex()
    getAccounts()
    |> Seq.iter (fun account ->
        if account.interests |> isNotNull
        then
            account.interests
            |> Seq.iter (fun interest -> interestsIndex.Set(BIKey(0,interest),account.id)))

let sortGroupDictionaries() =
    citySexGroups.['f'] <- citySexGroups.['f'].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    citySexGroups.['m'] <- citySexGroups.['m'].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    cityStatusGroups.[0] <- cityStatusGroups.[0].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    cityStatusGroups.[1] <- cityStatusGroups.[1].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    cityStatusGroups.[2] <- cityStatusGroups.[2].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    countrySexGroups.['f'] <- countrySexGroups.['f'].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    countrySexGroups.['m'] <- countrySexGroups.['m'].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    countryStatusGroups.[0] <- countryStatusGroups.[0].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    countryStatusGroups.[1] <- countryStatusGroups.[1].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    countryStatusGroups.[2] <- countryStatusGroups.[2].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))

let indexesRebuild() =
    buildBitMapIndex()
    sortGroupDictionaries()

let webApp =
    choose [
        GET >=> customGetRoutef
        POST >=> customPostRoutef
        setStatusCode 404 ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger)=
    Console.WriteLine(ex.ToString())
    setStatusCode 400

// ---------------------------------
// Config and Main
// ---------------------------------

let configureApp (app : IApplicationBuilder) =
    app.UseRequestCounter webApp
    app.UseGiraffeErrorHandler errorHandler |> ignore
    app.UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore

let configureKestrel (options : KestrelServerOptions) =
    options.ApplicationSchedulingMode <- Abstractions.Internal.SchedulingMode.Inline
    options.AllowSynchronousIO <- false

let loadData folder =
    let str = Path.Combine(folder,"options.txt")
                   |> File.ReadAllLines
    currentTs <- str.[0]
                   |> Int32.Parse


    Directory.EnumerateFiles(folder, "accounts_*.json")
                |> Seq.map (File.ReadAllText >> deserializeObjectUnsafe<AccountsUpd>)
                |> Seq.iter (fun accsUpd ->
                        accsUpd.accounts
                        |> Seq.iter (fun acc ->
                            accounts.[acc.id.Value] <- createAccount acc
                            Interlocked.Increment(&accountsNumber) |> ignore
                        )
                        GC.Collect(2)
                    )

    namesSerializeDictionary <- namesWeightDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> utf8 kv.Key))
    snamesSerializeDictionary <- snamesWeightDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> struct(kv.Key, utf8 kv.Key)))
    citiesSerializeDictionary <- citiesWeightDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> utf8 kv.Key))
    countriesSerializeDictionary <- countriesWeightDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> utf8 kv.Key))
    interestsSerializeDictionary <- interestsWeightDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> utf8 kv.Key))

    indexesRebuild() |> ignore

    let memSize = Process.GetCurrentProcess().PrivateMemorySize64/MB
    Console.WriteLine("Accounts {0}. Memory used {1}MB", accountsNumber, memSize)
    Console.WriteLine("Dictionaries names={0},cities={1},countries={2},interests={3},snames={4}",
        namesWeightDictionary.Count, citiesWeightDictionary.Count,countriesWeightDictionary.Count,interestsWeightDictionary.Count,snamesWeightDictionary.Count)


[<EntryPoint>]
let main argv =
    if Directory.Exists("./data")
    then Directory.Delete("./data",true)
    Directory.CreateDirectory("./data") |> ignore
    if File.Exists("/tmp/data/data.zip")
    then
        File.Copy("/tmp/data/options.txt","./data/options.txt")
        ZipFile.ExtractToDirectory("/tmp/data/data.zip","./data")
    else
        File.Copy("options.txt","./data/options.txt")
        ZipFile.ExtractToDirectory("data.zip","./data")
    loadData "./data"
    GC.Collect(2)
    GCTimer.runTimer buildBitMapIndex
    WebHostBuilder()
        .UseKestrel(Action<KestrelServerOptions> configureKestrel)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(Action<IServiceCollection> configureServices)
        .Build()
        .Run()
    0