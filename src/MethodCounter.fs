module HCup.MethodCounter

let accountFilterCount = ref 0
let accountsGroupCount = ref 0
let accountsRecommendCount = ref 0
let accountsSuggestCount = ref 0
let newAccountCount = ref 0
let updateAccountCount = ref 0
let addLikesCount = ref 0

let accountFilterTime = ref 0
let accountsGroupTime = ref 0
let accountsRecommendTime = ref 0
let accountsSuggestTime = ref 0
let newAccountTime = ref 0
let updateAccountTime = ref 0
let addLikesTime = ref 0

let mutable shouldRebuildIndex = false