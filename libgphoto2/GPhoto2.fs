namespace LibGPhoto2.GPhoto2

open System
open System.Threading.Tasks
open LibGPhoto2.GPhoto2.Native

type Camera(name: string) =
    let context = new libgphoto2.GPhoto2.GPContext();
    let mutable disposed = false
    let mutable handle = IntPtr.Zero
    let mutable widgets = ResizeArray<struct (string * CameraWidget)>()

    let loadConfig () =
        let mutable windowPtr = IntPtr.Zero
        let errorCode = gp_camera_get_config(handle, &windowPtr, context.handle)
        if errorCode <> ErrorCode.GpOk then raise <| GPhoto2Exception("Not able to get config of camera", errorCode)

        let rootWidget = new WindowCameraWidget(windowPtr)
        let rec addToWidgets path (w: CameraWidget) =
            let p = $"{path}/{w.Name}"
            widgets.Add(struct (p, w))

            match w with
            | :? SectionCameraWidget as w -> w.Children |> List.iter (addToWidgets p)
            | :? WindowCameraWidget as w -> w.Children |> List.iter (addToWidgets p)
            | _ -> ()

        rootWidget |> addToWidgets ""

    do
        let errorCode = gp_camera_new(&handle);
        if errorCode <> ErrorCode.GpOk then
            ("Couldn't create new Camera", errorCode)
            |> GPhoto2Exception
            |> raise

        loadConfig()

    override this.Finalize() = this.Dispose(false)
    member this.Dispose(disposing: bool) =
        if not disposed then
            this.Exit() |> ignore
            if disposing then context.Dispose()
            gp_camera_free handle |> ignore
            disposed <- true

    interface IDisposable with
        member this.Dispose() =
            this.Dispose(true)
            GC.SuppressFinalize this

    member this.Init() =
        let errorCode = gp_camera_init(handle, context.handle)
        if errorCode <> ErrorCode.GpOk then raise <| GPhoto2Exception("Couldn't set context to Camera and connect", errorCode)

    member this.Exit() : ErrorCode =
        if context <> null then
            gp_camera_exit(handle, context.handle)
        else
            ErrorCode.GpOk

    member _.Name = name

    member this.Capture() =
        let mutable path = CameraFilePath()
        let errorCode = gp_camera_capture(handle, CameraCaptureType.GP_CAPTURE_IMAGE, &path, context.handle)
        if errorCode <> ErrorCode.GpOk then raise <| GPhoto2Exception("Not able to capture image from camera", errorCode)

    member this.TriggerCapture() =
        let errorCode = gp_camera_trigger_capture(handle, context.handle)
        if errorCode <> ErrorCode.GpOk then raise <| GPhoto2Exception("Not able to trigger capture from camera", errorCode)

        Task.Run<unit>(fun () -> while this.WaitForEvent(200) <> CameraEventType.GP_EVENT_FILE_ADDED do ())

    member this.WaitForEvent(timeout: int) =
        let mutable eventType = CameraEventType.GP_EVENT_UNKNOWN
        let mutable eventData = 0n
        let errorCode = gp_camera_wait_for_event(handle, timeout, &eventType, &eventData, context.handle)
        if errorCode <> ErrorCode.GpOk then raise <| GPhoto2Exception("Not able to wait for event of camera", errorCode)
        eventType

    member this.Config = widgets

    member this.PortInfo
        with get () =
            let mutable info = IntPtr.Zero
            let portInfo = gp_camera_get_port_info(handle, &info)
            if portInfo <> ErrorCode.GpOk then
                raise <| GPhoto2Exception("Failure trying to get the port info from the camera", portInfo)
            new libgphoto2.GPhoto2.GPPortInfo(info)
        and set (value: libgphoto2.GPhoto2.GPPortInfo) =
            let errorCode = gp_camera_set_port_info(handle, value.handle)
            if errorCode <> ErrorCode.GpOk then
                raise <| GPhoto2Exception("Failure trying to set the port info for the camera", errorCode)

    // member this.GetCaptureTarget() =
    //     gp_camera_config
//
//   public byte[] CapturePreview()
//   {
//     int errorCode = GPhoto2.gp_camera_capture_preview(this.handle, this.camFile.handle, this.context.handle);
//     if (errorCode != 0)
//       throw new GPhoto2.GPhoto2Exception("Not able to capture a preview image from camera", errorCode);
//     return this.camFile.Get();
//   }
//
//   public byte[] Capture()
//   {
//     GPhoto2.CameraFilePath path;
//     int errorCode = GPhoto2.gp_camera_capture(this.handle, GPhoto2.CameraCaptureType.GP_CAPTURE_IMAGE, out path, this.context.handle);
//     if (errorCode != 0)
//       throw new GPhoto2.GPhoto2Exception("Not able to capture image from camera", errorCode);
//     this.FileGet(path);
//     byte[] numArray = this.camFile.Get();
//     this.FileDelete(path);
//     return numArray;
//   }
//
//   public Tuple<GPhoto2.CameraEventType, IntPtr> WaitForEvent(int timeout)
//   {
//     IntPtr eventdata = IntPtr.Zero;
//     GPhoto2.CameraEventType eventtype;
//     int errorCode = GPhoto2.gp_camera_wait_for_event(this.handle, timeout, out eventtype, out eventdata, this.context.handle);
//     if (errorCode != 0)
//       throw new GPhoto2.GPhoto2Exception("Error waiting for camera event", errorCode);
//     return new Tuple<GPhoto2.CameraEventType, IntPtr>(eventtype, eventdata);
//   }
//
//   public GPhoto2.CameraFile FileGet(GPhoto2.CameraFilePath path)
//   {
//     int errorCode = GPhoto2.gp_camera_file_get(this.handle, path.folder, path.name, GPhoto2.CameraFileType.GP_FILE_TYPE_NORMAL, this.camFile.handle, this.context.handle);
//     if (errorCode != 0)
//       throw new GPhoto2.GPhoto2Exception("Not able to get file from camera", errorCode);
//     return this.camFile;
//   }
//
//   public void FileDelete(GPhoto2.CameraFilePath path)
//   {
//     int errorCode = GPhoto2.gp_camera_file_delete(this.handle, path.folder, path.name, this.context.handle);
//     if (errorCode != 0)
//       throw new GPhoto2.GPhoto2Exception("Not able to delete file from camera", errorCode);
//   }
//
//   public GPhoto2.WindowCameraWidget Config
//   {
//     get
//     {
//       IntPtr window;
//       int config = GPhoto2.gp_camera_get_config(this.handle, out window, this.context.handle);
//       if (config != 0)
//         throw new GPhoto2.GPhoto2Exception("Not able to get root config widget", config);
//       return new GPhoto2.WindowCameraWidget(window);
//     }
//   }
//
//   public GPhoto2.CameraWidget GetSingleConfig(string name)
//   {
//     IntPtr widget;
//     int singleConfig = GPhoto2.gp_camera_get_single_config(this.handle, name, out widget, this.context.handle);
//     switch (singleConfig)
//     {
//       case -2:
//         throw new GPhoto2.WidgetNotFoundException(name);
//       case 0:
//         return GPhoto2.CameraWidget.NewFromType(widget);
//       default:
//         throw new GPhoto2.GPhoto2Exception("Not able to retrive widget from camera", singleConfig);
//     }
//   }
//
//   public void SetSingleConfig(GPhoto2.CameraWidget widget)
//   {
//     int errorCode = GPhoto2.gp_camera_set_single_config(this.handle, widget.Name, widget.handle, this.context.handle);
//     if (errorCode != 0)
//       throw new GPhoto2.GPhoto2Exception("Error setting the widget config on the camera", errorCode);
//   }
// }







  //
  //
  // public class CameraAbilitiesList : IDisposable
  // {
  //   private bool disposed;
  //   public IntPtr handle;
  //   private GPhoto2.GPContext context;
  //
  //   public CameraAbilitiesList()
  //   {
  //     int errorCode = GPhoto2.gp_abilities_list_new(ref this.handle);
  //     if (errorCode != 0)
  //       throw new GPhoto2.GPhoto2Exception("Not able to create new CameraAbilitiesList", errorCode);
  //   }
  //
  //   ~CameraAbilitiesList() => this.Dispose(false);
  //
  //   public void Dispose()
  //   {
  //     this.Dispose(true);
  //     GC.SuppressFinalize((object) this);
  //   }
  //
  //   protected virtual void Dispose(bool disposing)
  //   {
  //     if (this.disposed)
  //       return;
  //     int num = disposing ? 1 : 0;
  //     GPhoto2.gp_abilities_list_free(this.handle);
  //     this.disposed = true;
  //   }
  //
  //   public void Load(GPhoto2.GPContext context)
  //   {
  //     int errorCode = GPhoto2.gp_abilities_list_load(this.handle, context.handle);
  //     if (errorCode != 0)
  //       throw new GPhoto2.GPhoto2Exception("Not able to scan system for Camera drivers", errorCode);
  //     this.context = context;
  //   }
  //
  //   public GPhoto2.CameraList Detect(GPhoto2.GPPortInfoList infoList)
  //   {
  //     if (this.context == null)
  //       throw new Exception("Need context attached before detection, call Load() method first.");
  //     GPhoto2.CameraList cameraList = new GPhoto2.CameraList();
  //     int errorCode = GPhoto2.gp_abilities_list_detect(this.handle, infoList.handle, cameraList.handle, this.context.handle);
  //     if (errorCode != 0)
  //       throw new GPhoto2.GPhoto2Exception("Failure when trying to detect cameras", errorCode);
  //     return cameraList;
  //   }
  //
  //   public GPhoto2.CameraAbilities GetAbilities(int index)
  //   {
  //     GPhoto2.CameraAbilities abilities1;
  //     int abilities2 = GPhoto2.gp_abilities_list_get_abilities(this.handle, index, out abilities1);
  //     if (abilities2 != 0)
  //       throw new GPhoto2.GPhoto2Exception("Not able to get CameraAbilities", abilities2);
  //     return abilities1;
  //   }
  // }
  //
  // public class CameraFile : IDisposable
  // {
  //   private bool disposed;
  //   public IntPtr handle;
  //
  //   public CameraFile()
  //   {
  //     int errorCode = GPhoto2.gp_file_new(ref this.handle);
  //     if (errorCode != 0)
  //       throw new GPhoto2.GPhoto2Exception("Not able to create new CameraFile", errorCode);
  //   }
  //
  //   public CameraFile(IntPtr file) => this.handle = file;
  //
  //   ~CameraFile() => this.Dispose(false);
  //
  //   public void Dispose()
  //   {
  //     this.Dispose(true);
  //     GC.SuppressFinalize((object) this);
  //   }
  //
  //   protected virtual void Dispose(bool disposing)
  //   {
  //     if (this.disposed)
  //       return;
  //     int num = disposing ? 1 : 0;
  //     GPhoto2.gp_file_free(this.handle);
  //     this.disposed = true;
  //   }
  //
  //   public byte[] Get()
  //   {
  //     ulong size = 0;
  //     IntPtr zero = IntPtr.Zero;
  //     int dataAndSize = GPhoto2.gp_file_get_data_and_size(this.handle, ref zero, ref size);
  //     if (dataAndSize != 0)
  //       throw new GPhoto2.GPhoto2Exception("Not able to get size or data from preview image.", dataAndSize);
  //     byte[] destination = new byte[size];
  //     Marshal.Copy(zero, destination, 0, destination.Length);
  //     return destination;
  //   }
  // }
  //

  //
  // public class CameraList : IDisposable
  // {
  //   private bool disposed;
  //   public IntPtr handle;
  //
  //   public int Count => GPhoto2.gp_list_count(this.handle);
  //
  //   public CameraList()
  //   {
  //     int errorCode = GPhoto2.gp_list_new(ref this.handle);
  //     if (errorCode != 0)
  //       throw new GPhoto2.GPhoto2Exception("Not able to create a new CameraList", errorCode);
  //   }
  //
  //   ~CameraList() => this.Dispose(false);
  //
  //   public void Dispose()
  //   {
  //     this.Dispose(true);
  //     GC.SuppressFinalize((object) this);
  //   }
  //
  //   protected virtual void Dispose(bool disposing)
  //   {
  //     if (this.disposed)
  //       return;
  //     int num = disposing ? 1 : 0;
  //     GPhoto2.gp_list_free(this.handle);
  //     this.disposed = true;
  //   }
  //
  //   public string GetName(int index)
  //   {
  //     IntPtr zero = IntPtr.Zero;
  //     int name = GPhoto2.gp_list_get_name(this.handle, index, ref zero);
  //     if (name != 0)
  //       throw new GPhoto2.GPhoto2Exception("Error retriving name from list", name);
  //     return Marshal.PtrToStringAnsi(zero);
  //   }
  //
  //   public string GetValue(int index)
  //   {
  //     IntPtr zero = IntPtr.Zero;
  //     int errorCode = GPhoto2.gp_list_get_value(this.handle, index, ref zero);
  //     if (errorCode != 0)
  //       throw new GPhoto2.GPhoto2Exception("Error retriving value from list", errorCode);
  //     return Marshal.PtrToStringAnsi(zero);
  //   }
  // }
  //
  // public struct CameraText
  // {
  //   [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32768 /*0x8000*/)]
  //   public sbyte[] text;
  //
  //   public string Text => Utils.SByteArrayToString(this.text);
  // }
  //

  //
  // public class GPhoto2Exception : Exception
  // {
  //   private int errorCode;
  //
  //   public int ErrorCode => this.errorCode;
  //
  //   public GPhoto2Exception(int errorCode = -1) => this.errorCode = errorCode;
  //
  //   public GPhoto2Exception(string message, int errorCode = -1)
  //     : base(message)
  //   {
  //     this.errorCode = errorCode;
  //   }
  //
  //   public GPhoto2Exception(string message, Exception inner, int errorCode = -1)
  //     : base(message, inner)
  //   {
  //     this.errorCode = errorCode;
  //   }
  // }
  //
  // public class WidgetNotFoundException : GPhoto2.GPhoto2Exception
  // {
  //   public string Details { get; private set; }
  //
  //   public WidgetNotFoundException(string details)
  //     : base(-2)
  //   {
  //     this.Details = details;
  //   }
  //
  //   public WidgetNotFoundException(string message, string details)
  //     : base(message, -2)
  //   {
  //     this.Details = details;
  //   }
  //
  //   public WidgetNotFoundException(string message, Exception inner, string details)
  //     : base(message, inner, -2)
  //   {
  //     this.Details = details;
  //   }
  // }
  //
  // public class GPContext : IDisposable
  // {
  //   private bool disposed;
  //   public IntPtr handle;
  //
  //   public GPContext() => this.handle = GPhoto2.gp_context_new();
  //
  //   ~GPContext() => this.Dispose(false);
  //
  //   public void Dispose()
  //   {
  //     this.Dispose(true);
  //     GC.SuppressFinalize((object) this);
  //   }
  //
  //   protected virtual void Dispose(bool disposing)
  //   {
  //     if (this.disposed)
  //       return;
  //     int num = disposing ? 1 : 0;
  //     GPhoto2.gp_context_unref(this.handle);
  //     this.disposed = true;
  //   }
  // }
  //
  // public class GPPortInfo : IDisposable
  // {
  //   private bool disposed;
  //   public IntPtr handle;
  //
  //   public GPPortInfo()
  //   {
  //     int errorCode = GPhoto2.gp_port_info_new(out this.handle);
  //     if (errorCode != 0)
  //       throw new GPhoto2.GPhoto2Exception("Not able to create a new PortInfo object", errorCode);
  //   }
  //
  //   public GPPortInfo(IntPtr info) => this.handle = info;
  //
  //   ~GPPortInfo() => this.Dispose(false);
  //
  //   public void Dispose()
  //   {
  //     this.Dispose(true);
  //     GC.SuppressFinalize((object) this);
  //   }
  //
  //   protected virtual void Dispose(bool disposing)
  //   {
  //     if (this.disposed)
  //       return;
  //     int num = disposing ? 1 : 0;
  //     this.disposed = true;
  //   }
  //
  //   public string Name
  //   {
  //     get
  //     {
  //       IntPtr name1 = IntPtr.Zero;
  //       int name2 = GPhoto2.gp_port_info_get_name(this.handle, out name1);
  //       if (name2 != 0)
  //         throw new GPhoto2.GPhoto2Exception("Error getting name from PortInfo", name2);
  //       return Marshal.PtrToStringAnsi(name1);
  //     }
  //     set
  //     {
  //       IntPtr hglobalAnsi = Marshal.StringToHGlobalAnsi(value);
  //       int errorCode = GPhoto2.gp_port_info_set_name(this.handle, hglobalAnsi);
  //       if (errorCode != 0)
  //         throw new GPhoto2.GPhoto2Exception("Error setting name for PortInfo", errorCode);
  //       Marshal.FreeHGlobal(hglobalAnsi);
  //     }
  //   }
  //
  //   public string Path
  //   {
  //     get
  //     {
  //       IntPtr path1 = IntPtr.Zero;
  //       int path2 = GPhoto2.gp_port_info_get_path(this.handle, out path1);
  //       if (path2 != 0)
  //         throw new GPhoto2.GPhoto2Exception("Error getting path from PortInfo", path2);
  //       return Marshal.PtrToStringAnsi(path1);
  //     }
  //     set
  //     {
  //       IntPtr hglobalAnsi = Marshal.StringToHGlobalAnsi(value);
  //       int errorCode = GPhoto2.gp_port_info_set_path(this.handle, hglobalAnsi);
  //       if (errorCode != 0)
  //         throw new GPhoto2.GPhoto2Exception("Error setting path for PortInfo", errorCode);
  //       Marshal.FreeHGlobal(hglobalAnsi);
  //     }
  //   }
  //
  //   public GPhoto2.GPPortType Type
  //   {
  //     get
  //     {
  //       GPhoto2.GPPortType type1;
  //       int type2 = GPhoto2.gp_port_info_get_type(this.handle, out type1);
  //       if (type2 != 0)
  //         throw new GPhoto2.GPhoto2Exception("Error getting type from PortInfo", type2);
  //       return type1;
  //     }
  //     set
  //     {
  //       int errorCode = GPhoto2.gp_port_info_set_type(this.handle, value);
  //       if (errorCode != 0)
  //         throw new GPhoto2.GPhoto2Exception("Error setting type for PortInfo", errorCode);
  //     }
  //   }
  // }
  //
  // public class GPPortInfoList : IDisposable
  // {
  //   private bool disposed;
  //   public IntPtr handle;
  //
  //   public GPPortInfoList()
  //   {
  //     int errorCode = GPhoto2.gp_port_info_list_new(ref this.handle);
  //     if (errorCode != 0)
  //       throw new GPhoto2.GPhoto2Exception("Not able to create a new GPPortInfoList", errorCode);
  //   }
  //
  //   ~GPPortInfoList() => this.Dispose(false);
  //
  //   public void Dispose()
  //   {
  //     this.Dispose(true);
  //     GC.SuppressFinalize((object) this);
  //   }
  //
  //   protected virtual void Dispose(bool disposing)
  //   {
  //     if (this.disposed)
  //       return;
  //     int num = disposing ? 1 : 0;
  //     GPhoto2.gp_port_info_list_free(this.handle);
  //     this.disposed = true;
  //   }
  //
  //   public void Load()
  //   {
  //     int errorCode = GPhoto2.gp_port_info_list_load(this.handle);
  //     if (errorCode != 0)
  //       throw new GPhoto2.GPhoto2Exception("Not able to find Camera IO drivers", errorCode);
  //   }
  //
  //   public int LookupPath(string path)
  //   {
  //     int errorCode = GPhoto2.gp_port_info_list_lookup_path(this.handle, path.ToSByteArray());
  //     return errorCode != -5 ? errorCode : throw new GPhoto2.GPhoto2Exception("Unknown path/port provided", errorCode);
  //   }
  //
  //   public GPhoto2.GPPortInfo GetInfo(int n)
  //   {
  //     IntPtr info1;
  //     int info2 = GPhoto2.gp_port_info_list_get_info(this.handle, n, out info1);
  //     if (info2 != 0)
  //       throw new GPhoto2.GPhoto2Exception("Error getting PortInfo from list", info2);
  //     return new GPhoto2.GPPortInfo(info1);
  //   }
  // }
