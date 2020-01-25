module App

// open FSharp.Core
open Fable.Core
// open Fable.Core.JS
open Fable.Core.JsInterop
// open Fable.Promise
open Browser.Types
open Browser.Dom
open Blockly
open JupyterlabServices.__kernel_messages.KernelMessage
open JupyterlabServices.__kernel_kernel.Kernel

//tricky here: if we try to make collection of requires, F# complains they are different types unless we specify obj type
let mutable requires: obj array =
    [| JupyterlabApputils.ICommandPalette; JupyterlabNotebook.Tokens.Types.INotebookTracker |]

//https://stackoverflow.com/questions/47640263/how-to-extend-a-js-class-in-fable
[<Import("Widget", from = "@phosphor/widgets")>]
[<AbstractClass>]
type Widget() =
    class
        // only implementing the minimal members needed
        member val node: HTMLElement = null with get, set
        /// Guess only fires once after attach to DOM. onAfterShow is called after every display (e.g., switching tabs)
        abstract onAfterAttach: unit -> unit
        /// Using to resize blockly
        abstract onResize: PhosphorWidgets.Widget.ResizeMessage -> unit
    end

/// Might not be strictly necessary to wrap blockly in a widget
type BlocklyWidget(notebooks: JupyterlabNotebook.Tokens.INotebookTracker) as this =
    class
        inherit Widget()
        let generator = Blockly.python //Blockly.javascript

        do
            //inject intellisense dependency into Blockly toolbox
            Toolbox.notebooks <- notebooks

            //div to hold blockly
            let div = document.createElement ("div")
            div.setAttribute("style", "height: 480px; width: 600px;") //initial but will be immediately resized
            div.id <- "blocklyDiv" //for debug and to refer to during injection
            this.node.appendChild (div) |> ignore

            //div for buttons
            let buttonDiv =  document.createElement ("div")
            buttonDiv.id <- "buttonDiv"

            //button to trigger code generation
            let blocksToCodeButton = document.createElement ("button")
            blocksToCodeButton.innerText <- "Blocks to Code"
            blocksToCodeButton.addEventListener ("click", (fun _ -> this.RenderCode()))
            buttonDiv.appendChild( blocksToCodeButton) |> ignore

            //button to reverse xml to blocks
            let codeToBlocksButton = document.createElement ("button")
            codeToBlocksButton.innerText <- "Code to Blocks"
            codeToBlocksButton.addEventListener ("click", (fun _ -> this.RenderBlocks()))
            buttonDiv.appendChild( codeToBlocksButton) |> ignore

            //button for bug reports
            let bugReportButton = document.createElement ("button")
            bugReportButton.innerText <- "Report Bug"
            bugReportButton.addEventListener
                ("click",
                 (fun _ ->
                     let win =
                         Browser.Dom.window.``open``
                             ("https://github.com/aolney/fable-jupyterlab-blockly-extension/issues", "_blank")
                     win.focus()
                     ()))
            buttonDiv.appendChild( bugReportButton) |> ignore

            //append all buttons in div
            this.node.appendChild ( buttonDiv ) |> ignore

        member val workspace: Blockly.Workspace = null with get, set
        member val notHooked = true with get, set
        member this.Notebooks = notebooks

        member this.onKernelExecuted =
            PhosphorSignaling.Slot<IKernel, IIOPubMessage>(fun sender args ->
                // Browser.Dom.console.log( "kernel message: " + args.header.msg_type.ToString() )
                let messageType = args.header.msg_type.ToString()
                if messageType = "execute_input" then
                    Browser.Dom.console.log ("kernel executed code, updating intellisense")
                    Toolbox.UpdateAllIntellisense()
                true)

        /// Wait until widget shown to prevent injection from taking place before the DOM is ready
        /// Inject blockly into div and save blockly workspace to private member
        override this.onAfterAttach() =
            //try to register for code execution messages if we haven't already
            if this.notHooked then
                match this.Notebooks.currentWidget with
                | Some(widget) ->
                    match widget.session.kernel with
                    | Some(kernel) ->
                        let ikernel = kernel :?> IKernel
                        ikernel.iopubMessage.connect (this.onKernelExecuted, this) |> ignore
                        console.log ("Listening for kernel messages")
                        this.notHooked <- false
                    | None -> ()
                | None -> ()

            //set up blockly workspace
            this.workspace <-
                blockly.inject
                    (!^"blocklyDiv",
                     // Tricky: creatObj cannot be used here. Must use jsOptions to create POJO
                     jsOptions<Blockly.BlocklyOptions> (fun o -> o.toolbox <- !^Toolbox.toolbox |> Some)
                    // THIS FAILS!
                    // ~~ [
                    //     "toolbox" ==> ~~ [ "toolbox" ==> toolbox2 ] //TODO: using toolbox2 same as using empty string here
                    // ] :?> Object
                    )
            console.log ("blockly palette initialized")

        override this.onResize( msg : PhosphorWidgets.Widget.ResizeMessage ) =
          let blocklyDiv = document.getElementById("blocklyDiv")
          let buttonDiv = document.getElementById("buttonDiv")
          //adjust height for buttons; TODO be smarter about button height / see if we can be smarter with JupyterLab layouts
          let adjustedHeight = msg.height - 30.0 // buttonDiv.clientHeight
          //it seems we don't need parent offset; we can just use 0 
          //let parentOffset = blocklyDiv.offsetParent :?> HTMLElement
          // blocklyDiv.setAttribute("style", "position: absolute; top: " + parentOffset.offsetTop.ToString() + "px; left: " + parentOffset.offsetLeft.ToString() + "px; width: " + msg.width.ToString() + "px; height: " + adjustedHeight.ToString() + "px" )
          blocklyDiv.setAttribute("style", "position: absolute; top: " + "0" + "px; left: " + "0" + "px; width: " + msg.width.ToString() + "px; height: " + adjustedHeight.ToString() + "px" )
          buttonDiv.setAttribute("style", "position: absolute; top: " + adjustedHeight.ToString() + "px; left: " + "0" + "px; width: " + msg.width.ToString() + "px; height: " + "30" + "px" )
          blockly.svgResize( this.workspace :?> Blockly.WorkspaceSvg )
          ()

        member this.RenderBlocks() =
            if notebooks.activeCell <> null then
                //xml is always the last line of cell
                let xmlStringComment = notebooks.activeCell.model.value.text.Split('\n') |> Array.last
                Toolbox.decodeWorkspace (xmlStringComment.TrimStart([| '#' |]))
            else
                console.log ("unable to decode blocks")

        member this.RenderCode() =
            let code = generator.workspaceToCode (this.workspace)
            if notebooks.activeCell <> null then
                notebooks.activeCell.model.value.text <-
                    notebooks.activeCell.model.value.text + code + "\n#"
                    + Toolbox.encodeWorkspace() //append seems better than overwrite...
                console.log ("wrote to active cell:\n" + code + "\n")
            else
                console.log ("no cell active, flushed:\n" + code + "\n")
    end

let extension =
    createObj
        [ "id" ==> "jupyterlab_blockly_extension"
          "autoStart" ==> true
          "requires" ==> requires //
          //------------------------------------------------------------------------------------------------------------
          //NOTE: this **must** be wrapped in a Func, otherwise the arguments are tupled and Jupyter doesn't expect that
          //------------------------------------------------------------------------------------------------------------
          "activate" ==> System.Func<JupyterlabApplication.JupyterFrontEnd<JupyterlabApplication.LabShell>, JupyterlabApputils.ICommandPalette, JupyterlabNotebook.Tokens.INotebookTracker, unit>(fun app palette notebooks ->
                             console.log ("JupyterLab extension jupyterlab_blockly_extension is activated!")

                             //Create a blockly widget and place inside main area widget
                             let blocklyWidget = BlocklyWidget(notebooks)
                             let widget =
                                 JupyterlabApputils.Types.MainAreaWidget.Create
                                     (createObj [ "content" ==> blocklyWidget ])
                             widget.id <- "blockly-jupyterlab"
                             widget.title.label <- "Blockly Palette"
                             widget.title.closable <- true

                             // Add application command
                             let command = "blockly:open"
                             app.commands.addCommand
                                 (command,
                                  createObj
                                      [ "label" ==> "Blockly Jupyterlab Extension"
                                        "execute" ==> fun () ->
                                            if not <| widget.isAttached then
                                                match notebooks.currentWidget with
                                                | Some(c) ->
                                                    let options =
                                                        jsOptions<JupyterlabDocregistry.Registry.DocumentRegistry.IOpenOptions> (fun o ->
                                                            o.ref <- c.id |> Some
                                                            o.mode <-
                                                                PhosphorWidgets.DockLayout.InsertMode.SplitLeft |> Some)
                                                    c.context.addSibling (widget, options) |> ignore
                                                | None -> app.shell.add (widget, "main")
                                            app.shell.activateById (widget.id) ] :?> PhosphorCommands.CommandRegistry.ICommandOptions)
                             |> ignore
                             //Add command to palette
                             palette?addItem (createObj
                                                  [ "command" ==> command
                                                    "category" ==> "Blockly" ])) ]

exportDefault extension
