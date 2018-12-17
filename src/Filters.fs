module Filters

open System
open HCup.Models
open System.Collections.Generic
open Giraffe

type Filter = string -> Account -> bool

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

let filters: IDictionary<string, Filter> =
    dict [
        "sex_ex", sexEqFilter
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
    ]
