module MutationObserver

open Fable.Core
open Fable.Core.JsInterop
open Browser.Dom

// From https://gist.github.com/MangelMaxime/f9fbaa4d63bc649a283becfa7b6c3368
// Binding ported from https://github.com/fable-compiler/fable-import/tree/master/Browser

let nodeListToHTMLElements (nodeList:Browser.Types.NodeList) =
    seq {
        for i = 0 to nodeList.length - 1 do
            yield nodeList.[i] :?> Browser.Types.HTMLElement
    }

type [<AllowNullLiteral>] MutationEvent =
    inherit Browser.Types.Event
    abstract attrChange: float with get, set
    abstract attrName: string with get, set
    abstract newValue: string with get, set
    abstract prevValue: string with get, set
    abstract relatedNode: Browser.Types.Node with get, set
    abstract ADDITION: float with get, set
    abstract MODIFICATION: float with get, set
    abstract REMOVAL: float with get, set
    abstract initMutationEvent: typeArg: string * canBubbleArg: bool * cancelableArg: bool * relatedNodeArg: Browser.Types.Node * prevValueArg: string * newValueArg: string * attrNameArg: string * attrChangeArg: float -> unit

and MutationCallback = (ResizeArray<MutationRecord> -> MutationObserver -> unit)

and [<AllowNullLiteral>] MutationEventType =
    abstract prototype: MutationEvent with get, set
    abstract ADDITION: float with get, set
    abstract MODIFICATION: float with get, set
    abstract REMOVAL: float with get, set
    [<Emit("new $0($1...)")>] abstract Create: unit -> MutationEvent

and [<AllowNullLiteral>] MutationObserverInit =
        abstract childList: bool option with get, set
        abstract attributes: bool option with get, set
        abstract characterData: bool option with get, set
        abstract subtree: bool option with get, set
        abstract attributeOldValue: bool option with get, set
        abstract characterDataOldValue: bool option with get, set
        abstract attributeFilter: ResizeArray<string> option with get, set

and [<AllowNullLiteral>] MutationObserver =
    abstract disconnect: unit -> unit
    abstract observe: target: Browser.Types.Node * options: MutationObserverInit -> unit
    abstract takeRecords: unit -> ResizeArray<MutationRecord>

and [<AllowNullLiteral>] MutationObserverType =
    abstract prototype: MutationObserver with get, set
    [<Emit("new $0($1...)")>] abstract Create: callback: MutationCallback -> MutationObserver

and [<AllowNullLiteral>] MutationRecord =
    abstract addedNodes: Browser.Types.NodeList with get, set
    abstract attributeName: string with get, set
    abstract attributeNamespace: string with get, set
    abstract nextSibling: Browser.Types.Node with get, set
    abstract oldValue: string with get, set
    abstract previousSibling: Browser.Types.Node with get, set
    abstract removedNodes: Browser.Types.NodeList with get, set
    abstract target: Browser.Types.Node with get, set
    abstract ``type``: string with get, set

and [<AllowNullLiteral>] MutationRecordType =
    abstract prototype: MutationRecord with get, set
    [<Emit("new $0($1...)")>] abstract Create: unit -> MutationRecord

let [<Global>] MutationObserver: MutationObserverType = jsNative

let testMutationObserver () =
    document.body.insertAdjacentHTML("beforeEnd", "<ol/>")
    let ol = document.body.lastChild :?> Browser.Types.HTMLOListElement
    document.body.insertAdjacentHTML("beforeEnd", "<input value='3'/>")
    let volume = document.body.lastChild :?> Browser.Types.HTMLInputElement
    document.body.insertAdjacentHTML("beforeEnd", "<input value='add' type='button'/>")
    let button = document.body.lastChild
    window.scrollTo(0., document.body.scrollHeight)
    button.addEventListener("click", fun _ ->
        for i = 0 to (int volume.value) do
            let html = sprintf "<li>%i</li>" i
            ol.insertAdjacentHTML("beforeEnd", html);
        window.scrollTo(0., document.body.scrollHeight)
    )

    let o = MutationObserver.Create(fun mutations observer ->
        for mutation in mutations do
            console.log(mutation.addedNodes.length)
    )
    
    let options = jsOptions<MutationObserverInit>(fun o ->
        o.childList <- Some true
    )

    o.observe(ol, options)

testMutationObserver ()