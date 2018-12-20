module Filters

open System
open HCup.Models
open System.Collections.Generic
open Giraffe

type Filter = string -> Account -> bool

let timestampBase = DateTime(1970, 1, 1, 0, 0, 0, 0)
[<Literal>]
let secondsInYear = 31556926

let inline convertToDate timestamp =
    timestampBase.AddSeconds((float)timestamp)

let inline convertToTimestamp (date: DateTime) =
    (int)(date - timestampBase).TotalSeconds

let mutable currentTs = 0

let inline (=~) str1 str2 =
    String.Equals(str1, str2,StringComparison.Ordinal)

let sexEqFilter (value: string) =
    fun (acc: Account) -> acc.sex = value.[0]

let emailDomainFilter (value: string) =
    fun (acc: Account) ->
        if acc.email |> isNull
        then
            false
        else
            let atIndex = acc.email.IndexOf('@', StringComparison.Ordinal)
            acc.email.Substring(atIndex+1) =~ value

let emailLtFilter (value: string) =
    fun (acc: Account) ->
        acc.email < value

let emailGtFilter (value: string) =
    fun (acc: Account) ->
        acc.email > value

let statusEqFilter (value: string) =
    fun (acc: Account) ->
        acc.status =~ value

let statusNeqFilter (value: string) =
    fun (acc: Account) ->
        acc.status =~ value |> not

let firstNameEqFilter (value: string) =
    fun (acc: Account) ->
        acc.fname =~ value

let firstNameAnyFilter (value: string) =
    fun (acc: Account) ->
        value.Split(',') |> Array.exists (fun el -> el =~ acc.fname)

let firstNameNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.fname |> isNull
        else acc.fname |> isNotNull

let surnameEqFilter (value: string) =
    fun (acc: Account) ->
        acc.sname =~ value

let surnameStartsFilter (value: string) =
    fun (acc: Account) ->
        if acc.sname |> isNull
        then
            false
        else
            acc.sname.StartsWith(value)

let surnameNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.sname |> isNull
        else acc.sname |> isNotNull

let phoneCodeFilter (value: string) =
    fun (acc: Account) ->
        if acc.phone |> isNull
        then
            false
        else
            acc.phone.Substring(2,3) =~ value

let phoneNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.phone |> isNull
        else acc.phone |> isNotNull

let countryEqFilter (value: string) =
    fun (acc: Account) ->
        acc.country =~ value

let countryNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.country |> isNull
        else acc.country |> isNotNull

let cityEqFilter (value: string) =
    fun (acc: Account) ->
        acc.city =~ value

let cityAnyFilter (value: string) =
    fun (acc: Account) ->
        value.Split(',') |> Array.exists (fun el -> el =~ acc.city)

let cityNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.city |> isNull
        else acc.city |> isNotNull

let birthLtFilter (value: string) =
    fun (acc: Account) ->
        acc.birth < Int32.Parse(value)

let birthGtFilter (value: string) =
    fun (acc: Account) ->
        acc.birth > Int32.Parse(value)

let birthYearFilter (value: string) =
    fun (acc: Account) ->
        let yearStartSeconds = (int)(DateTime(Int32.Parse(value), 1, 1) - timestampBase).TotalSeconds
        acc.birth > yearStartSeconds && acc.birth < (yearStartSeconds + secondsInYear)

let interestsContainsFilter (value: string) =
    fun (acc: Account) ->
        acc.interests |> isNotNull
            && value.Split(',') |> Array.forall (fun interest -> acc.interests |> Array.exists (fun el -> el =~ interest))

let interestsAnyFilter (value: string) =
    fun (acc: Account) ->
        acc.interests |> isNotNull
            && value.Split(',') |> Array.exists (fun interest -> acc.interests |> Array.exists (fun el -> el =~ interest))

let likesContainsFilter (value: string) =
    fun (acc: Account) ->
        acc.likes |> isNotNull
            && value.Split(',') |> Array.forall (fun id -> acc.likes |> Array.exists (fun el -> el.id = Int32.Parse(id)))

let premiumNowFilter (value: string) =
    fun (acc: Account) ->
        (box acc.premium |> isNotNull)
            && acc.premium.finish > currentTs
            && acc.premium.start <= currentTs

let premiumNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then box acc.premium |> isNull
        else box acc.premium |> isNotNull

let filters: IDictionary<string, Filter> =
    dict [
        "sex_eq", sexEqFilter
        "email_domain", emailDomainFilter
        "email_lt", emailLtFilter
        "email_gt", emailGtFilter
        "status_eq", statusEqFilter
        "status_neq", statusNeqFilter
        "fname_eq", firstNameEqFilter
        "fname_any", firstNameAnyFilter
        "fname_null", firstNameNullFilter
        "sname_eq", surnameEqFilter
        "sname_starts", surnameStartsFilter
        "sname_null", surnameNullFilter
        "phone_code", phoneCodeFilter
        "phone_null", phoneNullFilter
        "country_eq", countryEqFilter
        "country_null", countryNullFilter
        "city_eq", cityEqFilter
        "city_any", cityAnyFilter
        "city_null", cityNullFilter
        "birth_lt", birthLtFilter
        "birth_gt", birthGtFilter
        "birth_year", birthYearFilter
        "interests_contains", interestsContainsFilter
        "interests_any", interestsAnyFilter
        "likes_contains", likesContainsFilter
        "premium_now", premiumNowFilter
        "premium_null", premiumNullFilter
    ]
