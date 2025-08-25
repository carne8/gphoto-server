namespace rec LibGPhoto2.GPhoto2

open System
open System.Runtime.InteropServices
open LibGPhoto2.GPhoto2.Native

type CameraWidget(handle: nativeint, madeInLib: bool) =
    let mutable disposed = false

    new(widget: nativeint) = new CameraWidget(widget, true)
    new() = new CameraWidget(nativeint 0, false)

    member _.Handle = handle

    override this.Finalize() = this.Dispose()

    interface IDisposable with
        member this.Dispose() =
            this.Dispose()
            GC.SuppressFinalize(this)

    member private this.Dispose() =
        if not disposed then
            if not madeInLib then gp_widget_unref(handle) |> ignore
            disposed <- true

    static member NewFromType(widgetPtr: nativeint) : CameraWidget =
        let mutable typ = Unchecked.defaultof<CameraWidgetType>
        let err = gp_widget_get_type(widgetPtr, &typ)
        if err <> ErrorCode.GpOk then
            raise (GPhoto2Exception("Error determining the widget type", err))
        match typ with
        | CameraWidgetType.GP_WIDGET_WINDOW -> new WindowCameraWidget(widgetPtr)
        | CameraWidgetType.GP_WIDGET_SECTION -> new SectionCameraWidget(widgetPtr)
        | CameraWidgetType.GP_WIDGET_TEXT -> new TextCameraWidget(widgetPtr)
        | CameraWidgetType.GP_WIDGET_RANGE -> new RangeCameraWidget(widgetPtr)
        | CameraWidgetType.GP_WIDGET_TOGGLE -> new ToggleCameraWidget(widgetPtr)
        | CameraWidgetType.GP_WIDGET_RADIO -> new RadioCameraWidget(widgetPtr)
        | CameraWidgetType.GP_WIDGET_MENU -> new MenuCameraWidget(widgetPtr)
        | CameraWidgetType.GP_WIDGET_BUTTON -> new ButtonCameraWidget(widgetPtr)
        | CameraWidgetType.GP_WIDGET_DATE -> new DateCameraWidget(widgetPtr)
        | _ -> new CameraWidget(widgetPtr)

    member this.Changed
        with get() =
            let err = gp_widget_changed(handle)
            if err < ErrorCode.GpOk then raise (GPhoto2Exception("Error checking if widget was changed", err))
            int err = 1
        and set(value: bool) =
            let err = gp_widget_set_changed(handle, if value then 1 else 0)
            if err < ErrorCode.GpOk then raise (GPhoto2Exception("Error telling the widget it changed", err))

    member this.Type
        with get() =
            let mutable typ = Unchecked.defaultof<CameraWidgetType>
            let err = gp_widget_get_type(handle, &typ)
            if err <> ErrorCode.GpOk then raise (GPhoto2Exception("Error retriving widget type", err))
            typ

    member this.Name
        with get() =
            let mutable namePtr = nativeint 0
            let err = gp_widget_get_name(handle, &namePtr)
            if err <> ErrorCode.GpOk then raise (GPhoto2Exception("Not able to get widget name", err))
            Marshal.PtrToStringAnsi(namePtr)
        and set(value: string) =
            let err = gp_widget_set_name(handle, value)
            if err <> ErrorCode.GpOk then raise (GPhoto2Exception("not able to set widget name", err))

    member this.Info
        with get() =
            let mutable infoPtr = nativeint 0
            let err = gp_widget_get_info(handle, &infoPtr)
            if err <> ErrorCode.GpOk then raise (GPhoto2Exception("Not able to get widget info", err))
            Marshal.PtrToStringAnsi(infoPtr)
        and set(value: string) =
            let err = gp_widget_set_info(handle, value)
            if err <> ErrorCode.GpOk then raise (GPhoto2Exception("not able to set widget info", err))

    member this.Label
        with get() =
            let mutable lblPtr = nativeint 0
            let err = gp_widget_get_label(handle, &lblPtr)
            if err <> ErrorCode.GpOk then raise (GPhoto2Exception("Not able to get widget label", err))
            Marshal.PtrToStringAnsi(lblPtr)

    member this.Id
        with get() =
            let mutable id = 0
            let err = gp_widget_get_id(handle, &id)
            if err <> ErrorCode.GpOk then raise (GPhoto2Exception("Not able to get widget id", err))
            id

    member this.Readonly
        with get() =
            let mutable ro = 0
            let err = gp_widget_get_readonly(handle, &ro)
            if err <> ErrorCode.GpOk then raise (GPhoto2Exception("not able to get widget readonly flag", err))
            ro = 1
        and set(value: bool) =
            let err = gp_widget_set_readonly(handle, if value then 1 else 0)
            if err <> ErrorCode.GpOk then raise (GPhoto2Exception("not able to set widget readonly flag", err))

    member this.Parent
        with get(): CameraWidget | null =
            let mutable parentPtr = nativeint 0
            let err = gp_widget_get_parent(handle, &parentPtr)
            if err <> ErrorCode.GpOk then raise (GPhoto2Exception("Not able to get widget parent", err))
            if parentPtr <> nativeint 0 then CameraWidget.NewFromType(parentPtr) else null

    member this.Root
        with get() =
            let mutable rootPtr = nativeint 0
            let err = gp_widget_get_root(handle, &rootPtr)
            if err <> ErrorCode.GpOk then raise (GPhoto2Exception("Not able to get root widget", err))
            CameraWidget.NewFromType(rootPtr)
    //
    // member this.TrySetValue(value: obj) =
    //     match this with
    //     | :? WindowCameraWidget -> Error "Window Camera Widget can't be set"
    //     | :? SectionCameraWidget -> Error "Section Camera Widget can't be set"
    //     | :? TextCameraWidget as w -> try w.Value <- unbox value; Ok () with e -> Error e.Message
    //     | :? RangeCameraWidget as w -> try w.Value <- unbox value; Ok () with e -> Error e.Message
    //     | :? ToggleCameraWidget as w -> try w.Value <- unbox value; Ok () with e -> Error e.Message
    //     | :? RadioCameraWidget as w -> try w.Value <- unbox value; Ok () with e -> Error e.Message
    //     | :? MenuCameraWidget as w -> try w.Value <- unbox value; Ok () with e -> Error e.Message
    //     | :? ButtonCameraWidget -> Error "Button Camera Widget can't be set"
    //     | :? DateCameraWidget as w -> try w.Value <- unbox value; Ok () with e -> Error e.Message
    //     | _ -> Error "Widget type unknown"

// --- ButtonCameraWidget ---
type ButtonCameraWidget =
    inherit CameraWidget

    new(widget: IntPtr) as this =
        { inherit CameraWidget(widget) }
        then
            if this.Type <> CameraWidgetType.GP_WIDGET_BUTTON then
                raise (ArgumentException("Supplied handle is not a Button widget"))

    new(label: string) =
        { inherit CameraWidget(IntPtr.Zero) }
        then
            let mutable handle = IntPtr.Zero
            let errorCode = gp_widget_new(CameraWidgetType.GP_WIDGET_BUTTON, label, &handle)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("error creating new Button widget", errorCode))
            handle <- handle

// --- ChoicesCameraWidget ---
type ChoicesCameraWidget =
    inherit CameraWidget

    internal new(widget: IntPtr) as this =
        { inherit CameraWidget(widget) }
        then
            if this.Type <> CameraWidgetType.GP_WIDGET_RADIO &&
               this.Type <> CameraWidgetType.GP_WIDGET_MENU then
                raise (ArgumentException("supplied handle is not for a Radio or Menu widget"))

    internal new() =
        { inherit CameraWidget(IntPtr.Zero) }

    member this.AddChoice(choice: string) =
        let errorCode = gp_widget_add_choice(this.Handle, choice)
        if errorCode <> ErrorCode.GpOk then
            raise (GPhoto2Exception("Error adding choice", errorCode))

    member this.CountChoices
        with get() =
            let errorCode = gp_widget_count_choices(this.Handle)
            if errorCode >= ErrorCode.GpOk then int errorCode
            else raise (GPhoto2Exception("Error counting the widget choices", errorCode))

    member this.GetChoice(choiceNumber: int) =
        let countChoices = this.CountChoices
        if choiceNumber < 0 || choiceNumber >= countChoices then
            raise (IndexOutOfRangeException("Bad index provided"))

        let mutable choice = IntPtr.Zero
        let errorCode = gp_widget_get_choice(this.Handle, choiceNumber, &choice)
        if errorCode <> ErrorCode.GpOk then
            raise (GPhoto2Exception("Error getting widget choice", errorCode))
        Marshal.PtrToStringAnsi(choice)

    member this.Choices
        with get() =
            let countChoices = this.CountChoices
            let choices = ResizeArray<string>(countChoices)
            for i in 0 .. countChoices - 1 do
                choices.Add(this.GetChoice(i))
            choices

    member this.Value
        with get() =
            let mutable ptr = IntPtr.Zero
            let errorCode = gp_widget_get_value(this.Handle, &ptr)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error getting widget choice", errorCode))
            Marshal.PtrToStringAnsi(ptr)

        and set (value: string) =
            if not (this.Choices.Contains(value)) then
                raise (ArgumentException("Provided value is not a valid choice for the widget", value))
            let hglobal = Marshal.StringToHGlobalAnsi(value)
            try
                let errorCode = gp_widget_set_value(this.Handle, hglobal)
                if errorCode <> ErrorCode.GpOk then
                    raise (GPhoto2Exception("Error setting widget choice", errorCode))
            finally
                Marshal.FreeHGlobal(hglobal)

// --- ContainerCameraWidget ---
type ContainerCameraWidget =
    inherit CameraWidget

    internal new(widget: IntPtr) as this =
        { inherit CameraWidget(widget) }
        then
            if this.Type <> CameraWidgetType.GP_WIDGET_WINDOW &&
               this.Type <> CameraWidgetType.GP_WIDGET_SECTION then
                raise (ArgumentException("Supplied handle is not a Container widget"))

    internal new() =
        { inherit CameraWidget(IntPtr.Zero) }

    member this.Children
        with get() =
            let countChildren = gp_widget_count_children(this.Handle)
            if countChildren < ErrorCode.GpOk then
                raise (GPhoto2Exception("Error getting children count", countChildren))

            [ for i in 0 .. int countChildren - 1 ->
                let mutable child = IntPtr.Zero
                let errorCode = gp_widget_get_child(this.Handle, i, &child)
                if errorCode <> ErrorCode.GpOk then
                    raise (GPhoto2Exception("Error getting child", errorCode))
                CameraWidget.NewFromType(child) ]

// --- DateCameraWidget ---
type DateCameraWidget =
    inherit CameraWidget

    internal new(widget: IntPtr) as this =
        { inherit CameraWidget(widget) }
        then
            if this.Type <> CameraWidgetType.GP_WIDGET_DATE then
                raise (ArgumentException("Supplied handle is not a Date widget"))

    new(label: string) =
        { inherit CameraWidget(IntPtr.Zero) }
        then
            let mutable handle = IntPtr.Zero
            let errorCode = gp_widget_new(CameraWidgetType.GP_WIDGET_DATE, label, &handle)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("error creating new Date widget", errorCode))
            handle <- handle

    member this.Value
        with get() =
            let mutable timestamp = 0n
            let errorCode = gp_widget_get_value(this.Handle, &timestamp)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error getting date value", errorCode))
            DateTimeOffset.FromUnixTimeSeconds(int64 timestamp).DateTime

        and set (value: DateTime) =
            let ts = int (DateTimeOffset(value).ToUnixTimeSeconds())
            let errorCode = gp_widget_set_value(this.Handle, ts)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error setting date value", errorCode))

// --- MenuCameraWidget ---
type MenuCameraWidget =
    inherit ChoicesCameraWidget

    new(widget: IntPtr) =
        { inherit ChoicesCameraWidget(widget) }

    new(label: string) =
        { inherit ChoicesCameraWidget() }
        then
            let mutable handle = IntPtr.Zero
            let errorCode = gp_widget_new(CameraWidgetType.GP_WIDGET_MENU, label, &handle)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error creating new Menu widget", errorCode))
            handle <- handle

// --- RadioCameraWidget ---
type RadioCameraWidget =
    inherit ChoicesCameraWidget

    new(widget: IntPtr) =
        { inherit ChoicesCameraWidget(widget) }

    new(label: string) =
        { inherit ChoicesCameraWidget() }
        then
            let mutable handle = IntPtr.Zero
            let errorCode = gp_widget_new(CameraWidgetType.GP_WIDGET_RADIO, label, &handle)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error creating new Radio widget", errorCode))
            handle <- handle

// --- RangeCameraWidget ---
type RangeCameraWidget =
    inherit CameraWidget

    new(widget: IntPtr) as this =
        { inherit CameraWidget(widget) }
        then
            if this.Type <> CameraWidgetType.GP_WIDGET_RANGE then
                raise (ArgumentException("Supplied handle is not a Range widget"))

    new(label: string) =
        { inherit CameraWidget(IntPtr.Zero) }
        then
            let mutable handle = IntPtr.Zero
            let errorCode = gp_widget_new(CameraWidgetType.GP_WIDGET_RANGE, label, &handle)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error creating new Range widget", errorCode))
            handle <- handle

    member this.GetRange() =
        let mutable min, max, increment = 0f, 0f, 0f
        let errorCode = gp_widget_get_range(this.Handle, &min, &max, &increment)
        if errorCode <> ErrorCode.GpOk then
            raise (GPhoto2Exception("Error getting range", errorCode))
        min, max, increment

    member this.Value
        with get() =
            let mutable v = 0f
            let errorCode = gp_widget_get_value_float(this.Handle, &v)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error getting range value", errorCode))
            v
        and set value =
            let errorCode = gp_widget_set_value_float(this.Handle, value)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error setting range value", errorCode))

// --- SectionCameraWidget ---
type SectionCameraWidget =
    inherit ContainerCameraWidget

    new(widget: IntPtr) =
        { inherit ContainerCameraWidget(widget) }

    new(label: string) =
        { inherit ContainerCameraWidget() }
        then
            let mutable handle = IntPtr.Zero
            let errorCode = gp_widget_new(CameraWidgetType.GP_WIDGET_SECTION, label, &handle)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error creating new Section widget", errorCode))
            handle <- handle

// --- TextCameraWidget ---
type TextCameraWidget =
    inherit CameraWidget

    new(widget: IntPtr) as this =
        { inherit CameraWidget(widget) }
        then
            if this.Type <> CameraWidgetType.GP_WIDGET_TEXT then
                raise (ArgumentException("Supplied handle is not a Text widget"))

    new(label: string) =
        { inherit CameraWidget(IntPtr.Zero) }
        then
            let mutable handle = IntPtr.Zero
            let errorCode = gp_widget_new(CameraWidgetType.GP_WIDGET_TEXT, label, &handle)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error creating new Text widget", errorCode))
            handle <- handle

    member this.Value
        with get() =
            let mutable ptr = IntPtr.Zero
            let errorCode = gp_widget_get_value(this.Handle, &ptr)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error getting text value", errorCode))
            Marshal.PtrToStringAnsi(ptr)

        and set (value: string) =
            let hglobal = Marshal.StringToHGlobalAnsi(value)
            try
                let errorCode = gp_widget_set_value(this.Handle, hglobal)
                if errorCode <> ErrorCode.GpOk then
                    raise (GPhoto2Exception("Error setting text value", errorCode))
            finally
                Marshal.FreeHGlobal(hglobal)

// --- ToggleCameraWidget ---
type ToggleCameraWidget =
    inherit CameraWidget

    new(widget: IntPtr) as this =
        { inherit CameraWidget(widget) }
        then
            if this.Type <> CameraWidgetType.GP_WIDGET_TOGGLE then
                raise (ArgumentException("Supplied handle is not a Toggle widget"))

    new(label: string) =
        { inherit CameraWidget(IntPtr.Zero) }
        then
            let mutable handle = IntPtr.Zero
            let errorCode = gp_widget_new(CameraWidgetType.GP_WIDGET_TOGGLE, label, &handle)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error creating new Toggle widget", errorCode))
            handle <- handle

    member this.Value
        with get() =
            let mutable value = 0
            let errorCode = gp_widget_get_value_int(this.Handle, &value)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error getting toggle value", errorCode))
            value <> 0

        and set (value: bool) =
            let intVal = if value then 1 else 0
            let errorCode = gp_widget_set_value_int(this.Handle, intVal)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error setting toggle value", errorCode))

// --- WindowCameraWidget ---
type WindowCameraWidget =
    inherit ContainerCameraWidget

    new(widget: IntPtr) =
        { inherit ContainerCameraWidget(widget) }

    new(label: string) =
        { inherit ContainerCameraWidget() }
        then
            let mutable handle = IntPtr.Zero
            let errorCode = gp_widget_new(CameraWidgetType.GP_WIDGET_WINDOW, label, &handle)
            if errorCode <> ErrorCode.GpOk then
                raise (GPhoto2Exception("Error creating new Window widget", errorCode))
            handle <- handle
