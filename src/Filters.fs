module Filters

open System
open HCup.Models
open System.Collections.Generic
open Giraffe

type Filter = (string*string) -> Account -> bool

let inline (=~) str1 str2 =
    String.Equals(str1, str2,StringComparison.Ordinal)

let sexFilter (predicate: string, value: string) =
    fun (acc: Account) -> acc.sex = value.[0]

let emailFilter (predicate: string, value: string) =
    match predicate with
    | "domain" ->
        fun (acc: Account) ->
            if acc.email |> isNull
            then
                false
            else
                let atIndex = acc.email.IndexOf('@', StringComparison.Ordinal)
                acc.email.Substring(atIndex+1) =~ value
    | "lt" ->
        fun (acc: Account) ->
            acc.email < value
    | "gt" ->
        fun (acc: Account) ->
            acc.email > value
    | _ -> failwith "Illegal email predicate"

let statusFilter (predicate: string, value: string) =
    match predicate with
    | "eq" ->
        fun (acc: Account) ->
            acc.status =~ value
    | "neq" ->
        fun (acc: Account) ->
            acc.status =~ value |> not
    | _ -> failwith "Illegal status predicate"

let firstNameFilter (predicate: string, value: string) =
    match predicate with
    | "eq" ->
        fun (acc: Account) ->
            acc.fname =~ value
    | "any" ->
        fun (acc: Account) ->
            value.Split(',') |> Array.exists (fun el -> el =~ acc.fname)
    | "null" ->
        fun (acc: Account) ->
            if value.[0] = '1'
            then acc.fname |> isNull
            else acc.fname |> isNotNull
    | _ -> failwith "Illegal first name predicate"

let surnameFilter (predicate: string, value: string) =
    match predicate with
    | "eq" ->
        fun (acc: Account) ->
            acc.sname =~ value
    | "starts" ->
        fun (acc: Account) ->
            if acc.sname |> isNull
            then
                false
            else
                acc.sname.StartsWith(value)
    | "null" ->
        fun (acc: Account) ->
             if value.[0] = '1'
             then acc.sname |> isNull
             else acc.sname |> isNotNull
    | _ -> failwith "Illegal surname predicate"

let phoneFilter (predicate: string, value: string) =
    match predicate with
    | "code" ->
        fun (acc: Account) ->
            if acc.phone |> isNull
            then
                false
            else
                acc.phone.Substring(2,3) =~ value
    | "null" ->
        fun (acc: Account) ->
             if value.[0] = '1'
             then acc.phone |> isNull
             else acc.phone |> isNotNull
    | _ -> failwith "Illegal phone predicate"

let filters: IDictionary<string, Filter> =
    dict [
        "sex", sexFilter
        "email", emailFilter
        "status", statusFilter
        "fname", firstNameFilter
        "sname", surnameFilter
    ]
