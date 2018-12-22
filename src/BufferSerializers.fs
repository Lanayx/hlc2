module HCup.BufferSerializers

open System
open System.Buffers
open System.Globalization
open System.IO
open System.Text

open FSharp.NativeInterop

open HCup.Models
open HCup.Helpers
open HCup

#nowarn "9"

open System.Collections
open Giraffe
open System.Text.RegularExpressions
open HCup.Dictionaries

let private utf8Encoding = Encoding.UTF8
let utf8 : string -> byte[] = utf8Encoding.GetBytes
let inline private writeArray (output : MemoryStream) array =
    output.Write(array, 0, array.Length)
let inline private stream buffer =
    new MemoryStream(buffer, 0, buffer.Length, true, true)

let inline private writeInt32 (output : MemoryStream) (number: int) =
    let numbersCount =
        if number >= 1_000_000_000 then 10
        elif number >= 100_000_000 then 9
        elif number >= 10_000_000 then 8
        elif number >= 1_000_000 then 7
        elif number >= 100_000 then 6
        elif number >= 10_000 then 5
        elif number >= 1_000 then 4
        elif number >= 100 then 3
        elif number >= 10 then 2
        else 1
    let buffer = NativePtr.stackalloc<byte> numbersCount
    let mutable num = number
    let loopMax = numbersCount - 1
    for i = loopMax downto 0 do
        NativePtr.set buffer i (byte (num % 10 + 48))
        num <- num / 10
    for i = 0 to loopMax do
        output.WriteByte (NativePtr.get buffer i)

let inline private writeInt32x (output : MemoryStream) (number: int32) =
    if number > 0
    then
        let numbersCount =
            if number >= 1_000_000_000 then 10
            elif number >= 100_000_000 then 9
            elif number >= 10_000_000 then 8
            elif number >= 1_000_000 then 7
            elif number >= 100_000 then 6
            elif number >= 10_000 then 5
            elif number >= 1_000 then 4
            elif number >= 100 then 3
            elif number >= 10 then 2
            else 1
        let buffer = NativePtr.stackalloc<byte> numbersCount
        let mutable num = number
        let loopMax = numbersCount - 1
        for i = loopMax downto 0 do
            NativePtr.set buffer i (byte (num % 10 + 48))
            num <- num / 10
        for i = 0 to loopMax do
            output.WriteByte (NativePtr.get buffer i)

    else
        let posNumber = -number
        let numbersCount =
            if posNumber >= 1_000_000_000 then 10
            elif posNumber >= 100_000_000 then 9
            elif posNumber >= 10_000_000 then 8
            elif posNumber >= 1_000_000 then 7
            elif posNumber >= 100_000 then 6
            elif posNumber >= 10_000 then 5
            elif posNumber >= 1_000 then 4
            elif posNumber >= 100 then 3
            elif posNumber >= 10 then 2
            else 1
        let buffer = NativePtr.stackalloc<byte> numbersCount
        let mutable num = posNumber
        let loopMax = numbersCount - 1
        for i = loopMax downto 0 do
            NativePtr.set buffer i (byte (num % 10 + 48))
            num <- num / 10
        output.WriteByte(byte '-')
        for i = 0 to loopMax do
            output.WriteByte (NativePtr.get buffer i)

let inline private writeString (output : MemoryStream) (str: string) =
    let buffer = ArrayPool.Shared.Rent (str.Length*2)
    let written = utf8Encoding.GetBytes(str,0,str.Length, buffer,0)
    output.Write(buffer, 0, written)
    ArrayPool.Shared.Return buffer

let inline private writeChar (output : MemoryStream) (chr: char) =
    output.WriteByte((byte)chr)

let private writeFloat (output : MemoryStream) (value: float) =
     let zeroByte = (byte)'0';
     if value = 0.0
     then
        output.WriteByte(zeroByte);output.WriteByte((byte)'.');output.WriteByte(zeroByte);
     else
         let intValue = (int)value
         writeInt32 output intValue
         output.WriteByte((byte)'.')
         let mutable decimalValue = (int)(Math.Round(value*100000.0, MidpointRounding.AwayFromZero)) - intValue*100000
         if decimalValue = 0
         then output.WriteByte(zeroByte)
         else
             let mutable temp = decimalValue
             while temp < 10000 do
                 temp <- temp*10
                 output.WriteByte(zeroByte)
             while decimalValue % 10 = 0 do
                 decimalValue <- decimalValue / 10
             writeInt32 output decimalValue



let fieldsMap =
    dict [
        "sex_eq", AccountField.Sex
        "email_domain", AccountField.Email
        "email_lt", AccountField.Email
        "email_gt", AccountField.Email
        "status_eq", AccountField.Status
        "status_neq", AccountField.Status
        "fname_eq", AccountField.Firstname
        "fname_any", AccountField.Firstname
        "fname_null", AccountField.Firstname
        "sname_eq", AccountField.Surname
        "sname_starts", AccountField.Surname
        "sname_null", AccountField.Surname
        "phone_code", AccountField.Phone
        "phone_null", AccountField.Phone
        "country_eq", AccountField.Country
        "country_null", AccountField.Country
        "city_eq", AccountField.City
        "city_any", AccountField.City
        "city_null", AccountField.City
        "birth_lt", AccountField.Birth
        "birth_gt", AccountField.Birth
        "birth_year", AccountField.Birth
        "interests_contains", AccountField.Interests
        "interests_any", AccountField.Interests
        "likes_contains", AccountField.Likes
        "premium_now", AccountField.Premium
        "premium_null", AccountField.Premium
    ]

let private ``{"accounts":[`` = utf8 "{\"accounts\":["
let private ``{"groups":[`` = utf8 "{\"groups\":["
let private ``},{`` = utf8 "},{"
let private ``]}`` = utf8 "]}"
let private ``":`` = utf8 "\":"
let private ``",`` = utf8 "\","
let private ``null`` = utf8 "null"

let private ``"email":"`` = utf8 "\"email\":\""
let private ``","id":`` = utf8 "\",\"id\":"


let private ``,"fname":"`` = utf8 ",\"fname\":\""
let private ``,"sname":"`` = utf8 ",\"sname\":\""
let private ``,"interests":[`` = utf8 ",\"interests\":["
let private ``,"status":"`` = utf8 ",\"status\":\""
let private ``,"premium":{`` = utf8 ",\"premium\":{"
let private ``"start":`` = utf8 "\"start\":"
let private ``,"finish":`` = utf8 ",\"finish\":"
let private ``,"sex":"`` = utf8 ",\"sex\":\""
let private ``,"phone":"`` = utf8 ",\"phone\":\""
let private ``,"likes":[`` = utf8 ",\"likes\":["
let private ``{"ts":`` = utf8 "{\"ts\":"
let private ``,"id":`` = utf8 ",\"id\":"
let private ``,"birth":`` = utf8 ",\"birth\":"
let private ``,"city":"`` = utf8 ",\"city\":\""
let private ``,"country":"`` = utf8 ",\"country\":\""
let private ``"count":`` = utf8 "\"count\":"

let freeStringStatus = utf8 "\u0441\u0432\u043e\u0431\u043e\u0434\u043d\u044b"
let complexStringStatus = utf8 "\u0432\u0441\u0451 \u0441\u043b\u043e\u0436\u043d\u043e"
let occupiedStringStatus = utf8 "\u0437\u0430\u043d\u044f\u0442\u044b"


let getStatusString status =
    let x=
        match status with
        | Helpers.freeStatus -> freeStringStatus
        | Helpers.complexStatus -> complexStringStatus
        | Helpers.occupiedStatus -> occupiedStringStatus
        | _ -> failwith "Invalid int status"
    x

let writeField (field_predicate: string, acc: Account, output: MemoryStream) =
    let fieldType = fieldsMap.[field_predicate]
    match fieldType with
    | AccountField.Email -> ()
    | AccountField.Firstname ->
        if acc.fname <> 0L
        then
            writeArray output ``,"fname":"``
            writeArray output (namesSerializeDictionary.[acc.fname])
            writeChar output '"'
    | AccountField.Surname ->
        if acc.sname |> isNotNull
        then
            writeArray output ``,"sname":"``
            writeString output acc.sname
            writeChar output '"'
    | AccountField.Interests ->
        ()
    | AccountField.Status ->
        writeArray output ``,"status":"``
        writeArray output (getStatusString acc.status)
        writeChar output '"'
    | AccountField.Premium ->
        if (box acc.premium) |> isNotNull
        then
            writeArray output ``,"premium":{``
            writeArray output ``"start":``
            writeInt32 output acc.premium.start
            writeArray output ``,"finish":``
            writeInt32 output acc.premium.finish
            writeChar output '}'
    | AccountField.Sex ->
        writeArray output ``,"sex":"``
        writeChar output acc.sex
        writeChar output '"'
    | AccountField.Phone ->
        if acc.phone |> isNotNull
        then
            writeArray output ``,"phone":"``
            writeString output acc.phone
            writeChar output '"'
    | AccountField.Likes ->
        ()
    | AccountField.Birth ->
        writeArray output ``,"birth":``
        writeInt32x output acc.birth
    | AccountField.City ->
        if acc.city <> 0L
        then
            writeArray output ``,"city":"``
            writeArray output (citiesSerializeDictionary.[acc.city])
            writeChar output '"'
    | AccountField.Country ->
        if acc.country <> 0L
        then
            writeArray output ``,"country":"``
            writeArray output (countriesSerializeDictionary.[acc.country])
            writeChar output '"'
    | _ -> ()

let getAccsSize (accs: Account[], field_predicates: string[]) =
    let mutable baseSize = 40 * accs.Length * (2+field_predicates.Length)
    baseSize

let serializeAccounts (accs: Account[], field_predicates: string[]): MemoryStream =
    let array = ArrayPool.Shared.Rent (50 + getAccsSize(accs, field_predicates))
    let output = stream array
    writeArray output ``{"accounts":[``
    let mutable start = true
    for acc in accs do
        if start
        then
            start <- false
            writeChar output '{'
        else
            writeArray output ``},{``

        writeArray output ``"email":"``
        writeString output acc.email
        writeArray output ``","id":``
        writeInt32 output acc.id

        for field_predicate in field_predicates do
            writeField(field_predicate, acc, output)
    if start |> not
    then writeChar output '}'
    writeArray output ``]}``
    output


let serializeGroups<'T> (groups: ('T*int)[], groupName: string, writeValue): MemoryStream =
    let array = ArrayPool.Shared.Rent (50 + 60 * groups.Length)
    let output = stream array
    writeArray output ``{"groups":[``
    let mutable start = true
    for (value, count) in groups do
        if start
        then
            start <- false
            writeChar output '{'
        else
            writeArray output ``},{``
        writeChar output '"'
        writeString output groupName
        writeArray output ``":``
        writeValue output value
        writeChar output ','
        writeArray output ``"count":``
        writeInt32 output count
    if start |> not
    then writeChar output '}'
    writeArray output ``]}``
    output

let serializeGroupsCity (groups: (int64*int)[], groupName: string): MemoryStream =
    let writeValue output value =
        if value <> 0L
        then
            writeChar output '"'
            writeArray output (citiesSerializeDictionary.[value])
            writeChar output '"'
        else
            writeArray output ``null``
    serializeGroups (groups, groupName, writeValue)

let serializeGroupsInterests (groups: (int64*int)[], groupName: string): MemoryStream =
    let writeValue output value =
        writeChar output '"'
        writeArray output (interestsSerializeDictionary.[value])
        writeChar output '"'
    serializeGroups (groups, groupName, writeValue)

let serializeGroupsCountry (groups: (int64*int)[], groupName: string): MemoryStream =
    let writeValue output value =
        if value <> 0L
        then
            writeChar output '"'
            writeArray output (countriesSerializeDictionary.[value])
            writeChar output '"'
        else
            writeArray output ``null``
    serializeGroups (groups, groupName, writeValue)

let serializeGroupsSex (groups: (char*int)[], groupName: string): MemoryStream =
    let writeValue output value =
        writeChar output '"'
        writeChar output value
        writeChar output '"'
    serializeGroups (groups, groupName, writeValue)

let serializeGroupsStatus (groups: (int*int)[], groupName: string): MemoryStream =
    let writeValue output value =
        writeChar output '"'
        writeArray output (getStatusString value)
        writeChar output '"'
    serializeGroups (groups, groupName, writeValue)

let serializeGroups2<'T> (groups: ((int64*'T)*int)[], writeValue1, writeValue2): MemoryStream =
    let array = ArrayPool.Shared.Rent (50 + 80 * groups.Length)
    let output = stream array
    writeArray output ``{"groups":[``
    let mutable start = true
    for ((value1,value2), count) in groups do
        if start
        then
            start <- false
            writeChar output '{'
        else
            writeArray output ``},{``
        writeValue1 output value1
        writeValue2 output value2
        writeArray output ``"count":``
        writeInt32 output count
    if start |> not
    then writeChar output '}'
    writeArray output ``]}``
    output

let serializeGroups2Sex (groups: ((int64*char)*int)[], groupName1: string, groupName2: string): MemoryStream =
    let dictionary =
        if (groupName1 =~ "country")
        then countriesSerializeDictionary
        else citiesSerializeDictionary
    let writeValue1 output value =
        if value <> 0L
        then
            writeChar output '"'
            writeString output groupName1
            writeArray output ``":``
            writeChar output '"'
            writeArray output (dictionary.[value])
            writeArray output ``",``
    let writeValue2 output value =
        writeChar output '"'
        writeString output groupName2
        writeArray output ``":``
        writeChar output '"'
        writeChar output value
        writeArray output ``",``
    serializeGroups2 (groups, writeValue1, writeValue2)

let serializeGroups2Status (groups: ((int64*int)*int)[], groupName1: string, groupName2: string): MemoryStream =
    let dictionary =
        if (groupName1 =~ "country")
        then countriesSerializeDictionary
        else citiesSerializeDictionary
    let writeValue1 output value =
        if value <> 0L
        then
            writeChar output '"'
            writeString output groupName1
            writeArray output ``":``
            writeChar output '"'
            writeArray output (dictionary.[value])
            writeArray output ``",``
    let writeValue2 output value =
        writeChar output '"'
        writeString output groupName2
        writeArray output ``":``
        writeChar output '"'
        writeArray output (getStatusString value)
        writeArray output ``",``
    serializeGroups2 (groups, writeValue1, writeValue2)