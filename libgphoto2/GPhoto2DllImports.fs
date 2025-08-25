module LibGPhoto2.GPhoto2.Native

open System.Runtime.InteropServices

type CameraCaptureType =
    | GP_CAPTURE_IMAGE = 0
    | GP_CAPTURE_MOVIE = 1
    | GP_CAPTURE_SOUND = 2

type CameraDriverStatus =
    | GP_DRIVER_STATUS_PRODUCTION = 0
    | GP_DRIVER_STATUS_TESTING = 1
    | GP_DRIVER_STATUS_EXPERIMENTAL = 2
    | GP_DRIVER_STATUS_DEPRECATED = 3

type CameraEventType =
    | GP_EVENT_UNKNOWN = 0
    | GP_EVENT_TIMEOUT = 1
    | GP_EVENT_FILE_ADDED = 2
    | GP_EVENT_FOLDER_ADDED = 3
    | GP_EVENT_CAPTURE_COMPLETE = 4
    | GP_EVENT_FILE_CHANGED = 5

type CameraFileOperation =
    | GP_FILE_OPERATION_NONE = 0
    | GP_FILE_OPERATION_DELETE = 2
    | GP_FILE_OPERATION_PREVIEW = 8
    | GP_FILE_OPERATION_RAW = 16 // 0x00000010
    | GP_FILE_OPERATION_AUDIO = 32 // 0x00000020
    | GP_FILE_OPERATION_EXIF = 64 // 0x00000040

type CameraFileType =
    | GP_FILE_TYPE_PREVIEW = 0
    | GP_FILE_TYPE_NORMAL = 1
    | GP_FILE_TYPE_RAW = 2
    | GP_FILE_TYPE_AUDIO = 3
    | GP_FILE_TYPE_EXIF = 4
    | GP_FILE_TYPE_METADATA = 5

type CameraFolderOperation =
    | GP_FOLDER_OPERATION_NONE = 0
    | GP_FOLDER_OPERATION_DELETE_ALL = 1
    | GP_FOLDER_OPERATION_PUT_FILE = 2
    | GP_FOLDER_OPERATION_MAKE_DIR = 4
    | GP_FOLDER_OPERATION_REMOVE_DIR = 8

type CameraOperation =
    | GP_OPERATION_NONE = 0
    | GP_OPERATION_CAPTURE_IMAGE = 1
    | GP_OPERATION_CAPTURE_VIDEO = 2
    | GP_OPERATION_CAPTURE_AUDIO = 4
    | GP_OPERATION_CAPTURE_PREVIEW = 8
    | GP_OPERATION_CONFIG = 16 // 0x00000010
    | GP_OPERATION_TRIGGER_CAPTURE = 32 // 0x00000020

type CameraWidgetType =
    | GP_WIDGET_WINDOW = 0
    | GP_WIDGET_SECTION = 1
    | GP_WIDGET_TEXT = 2
    | GP_WIDGET_RANGE = 3
    | GP_WIDGET_TOGGLE = 4
    | GP_WIDGET_RADIO = 5
    | GP_WIDGET_MENU = 6
    | GP_WIDGET_BUTTON = 7
    | GP_WIDGET_DATE = 8

type GPhotoDeviceType =
    | GP_DEVICE_STILL_CAMERA = 0
    | GP_DEVICE_AUDIO_PLAYER = 1

type GPPortType =
    | GP_PORT_NONE = 0
    | GP_PORT_SERIAL = 1
    | GP_PORT_USB = 4
    | GP_PORT_DISK = 8
    | GP_PORT_PTPIP = 16 // 0x00000010
    | GP_PORT_USB_DISK_DIRECT = 32 // 0x00000020
    | GP_PORT_USB_SCSI = 64 // 0x00000040

[<Struct; StructLayout(LayoutKind.Sequential, CharSet=CharSet.Ansi)>]
type CameraFilePath =
  [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 128 (*0x80*))>]
  val mutable name: sbyte[]
  [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024 (*0x0400*))>]
  val mutable folder: sbyte[]

  member this.Name = libgphoto2.Utils.SByteArrayToString(this.name)
  member this.Folder = libgphoto2.Utils.SByteArrayToString(this.folder)

[<Struct>]
type CameraAbilities =
    [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 128 (*0x80*))>]
    val model: sbyte[]
    val status: CameraDriverStatus
    val port: GPPortType
    [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 64 (*0x40*))>]
    val speed: int[]
    val operations: CameraOperation
    val file_operations: CameraFileOperation
    val folder_operations: CameraFolderOperation
    val usb_vendor: int
    val usb_product: int
    val usb_class: int
    val usb_subclass: int
    val usb_protocol: int
    [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024 (*0x0400*))>]
    val library: sbyte[]
    [<MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024 (*0x0400*))>]
    val id: sbyte[]
    val device_type: GPhotoDeviceType
    val reserved2: int
    val reserved3: int
    val reserved4: int
    val reserved5: int
    val reserved6: int
    val reserved7: int
    val reserved8: int


type ErrorCode =
    | GpOk = 0
    | GpError = -1
    | GpErrorBadParameters = -2
    | GpErrorNoMemory = -3
    | GpErrorLibrary = -4
    | GpErrorUnknownPort = -5
    | GpErrorNotSupported = -6
    | GpErrorIo = -7
    | GpErrorFixedLimitExceeded = -8
    | GpErrorTimeout = -10
    | GpErrorIoSupportedSerial = -20
    | GpErrorIoSupportedUsb = -21
    | GpErrorIoInit = -31
    | GpErrorIoRead = -34
    | GpErrorIoWrite = -35
    | GpErrorIoUpdate = -37
    | GpErrorIoSerialSpeed = -41
    | GpErrorIoUsbClearHalt = -51
    | GpErrorIoUsbFind = -52
    | GpErrorIoUsbClaim = -53
    | GpErrorIoLock = -60
    | GpErrorHal = -70
    | GpErrorCorruptedData = -102
    | GpErrorFileExists = -103
    | GpErrorModelNotFound = -105
    | GpErrorDirectoryNotFound = -107
    | GpErrorFileNotFound = -108
    | GpErrorDirectoryExists = -109
    | GpErrorCameraBusy = -110
    | GpErrorPathNotAbsolute = -111
    | GpErrorCancel = -112
    | GpErrorCameraError = -113
    | GpErrorOsFailure = -114
    | GpErrorNoSpace = -115

exception GPhoto2Exception of string * ErrorCode

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_new(nativeint& camera)

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_free(nativeint camera)

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_init(nativeint camera, nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_exit(nativeint camera, nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_capture_preview(nativeint camera, nativeint file, nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_capture(nativeint camera, CameraCaptureType typ, CameraFilePath& path, nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_wait_for_event(
    nativeint camera,
    int timeout,
    CameraEventType& eventtype,
    nativeint& eventdata,
    nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_file_get(
    nativeint camera,
    sbyte[] folder,
    sbyte[] filename,
    CameraFileType typ,
    nativeint camera_file,
    nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_file_delete(
    nativeint camera,
    sbyte[] folder,
    sbyte[] filename,
    nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_autodetect(nativeint list, nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_get_port_info(nativeint camera, nativeint& info)

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_set_port_info(nativeint camera, nativeint info)

[<DllImport("gphoto2", CharSet=CharSet.Ansi, CallingConvention=CallingConvention.Cdecl)>]
extern ErrorCode gp_camera_get_single_config(
    nativeint camera,
    [<MarshalAs(UnmanagedType.LPStr)>] string name,
    nativeint& widget,
    nativeint context)

[<DllImport("gphoto2", CharSet=CharSet.Ansi, CallingConvention=CallingConvention.Cdecl)>]
extern ErrorCode gp_camera_set_single_config(
    nativeint camera,
    [<MarshalAs(UnmanagedType.LPStr)>] string name,
    nativeint widget,
    nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_get_config(nativeint camera, nativeint& window, nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_camera_trigger_capture(nativeint camera, nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_abilities_list_new(nativeint& list)

[<DllImport("gphoto2")>]
extern ErrorCode gp_abilities_list_free(nativeint list)

[<DllImport("gphoto2")>]
extern ErrorCode gp_abilities_list_load(nativeint list, nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_abilities_list_detect(nativeint list, nativeint info_list, nativeint l, nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_abilities_list_get_abilities(nativeint list, int index, CameraAbilities& abilities)

[<DllImport("gphoto2")>]
extern ErrorCode gp_file_new(nativeint& file)

[<DllImport("gphoto2")>]
extern ErrorCode gp_file_free(nativeint file)

[<DllImport("gphoto2")>]
extern ErrorCode gp_file_get_data_and_size(nativeint file, nativeint& data, uint64& size)

[<DllImport("gphoto2")>]
extern ErrorCode gp_list_new(nativeint& list)

[<DllImport("gphoto2")>]
extern ErrorCode gp_list_free(nativeint list)

[<DllImport("gphoto2")>]
extern ErrorCode gp_list_count(nativeint list)

[<DllImport("gphoto2")>]
extern ErrorCode gp_list_get_name(nativeint list, int index, nativeint& name)

[<DllImport("gphoto2")>]
extern ErrorCode gp_list_get_value(nativeint list, int index, nativeint& value)

[<DllImport("gphoto2")>]
extern nativeint gp_context_new()

[<DllImport("gphoto2")>]
extern void gp_context_ref(nativeint context)

[<DllImport("gphoto2")>]
extern void gp_context_unref(nativeint context)

[<DllImport("gphoto2")>]
extern ErrorCode gp_port_info_new(nativeint& info)

[<DllImport("gphoto2")>]
extern ErrorCode gp_port_info_get_name(nativeint info, nativeint& name)

[<DllImport("gphoto2")>]
extern ErrorCode gp_port_info_set_name(nativeint info, nativeint name)

[<DllImport("gphoto2")>]
extern ErrorCode gp_port_info_get_path(nativeint info, nativeint& path)

[<DllImport("gphoto2")>]
extern ErrorCode gp_port_info_set_path(nativeint info, nativeint name)

[<DllImport("gphoto2")>]
extern ErrorCode gp_port_info_get_type(nativeint info, GPPortType& typ)

[<DllImport("gphoto2")>]
extern ErrorCode gp_port_info_set_type(nativeint info, GPPortType typ)

[<DllImport("gphoto2")>]
extern ErrorCode gp_port_info_list_new(nativeint& list)

[<DllImport("gphoto2")>]
extern ErrorCode gp_port_info_list_free(nativeint list)

[<DllImport("gphoto2")>]
extern ErrorCode gp_port_info_list_load(nativeint list)

[<DllImport("gphoto2", CallingConvention=CallingConvention.Cdecl)>]
extern ErrorCode gp_port_info_list_lookup_path(nativeint list, sbyte[] path)

[<DllImport("gphoto2")>]
extern ErrorCode gp_port_info_list_get_info(nativeint list, int n, nativeint& info)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_new(CameraWidgetType typ, [<MarshalAs(UnmanagedType.LPStr)>] string label, nativeint& widget)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_ref(nativeint widget)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_unref(nativeint widget)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_changed(nativeint widget)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_set_changed(nativeint widget, int changed)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_get_type(nativeint widget, CameraWidgetType& typ)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_get_value(nativeint widget, nativeint& value)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_set_value(nativeint widget, nativeint value)

[<DllImport("gphoto2", EntryPoint="gp_widget_get_value")>]
extern ErrorCode gp_widget_get_value_int(nativeint widget, int& value)

[<DllImport("gphoto2", EntryPoint="gp_widget_set_value")>]
extern ErrorCode gp_widget_set_value_int(nativeint widget, int value)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_get_label(nativeint widget, nativeint& label)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_get_name(nativeint widget, nativeint& name)

[<DllImport("gphoto2", CharSet=CharSet.Ansi, CallingConvention=CallingConvention.Cdecl)>]
extern ErrorCode gp_widget_set_name(nativeint widget, [<MarshalAs(UnmanagedType.LPStr)>] string name)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_get_info(nativeint widget, nativeint& info)

[<DllImport("gphoto2", CharSet=CharSet.Ansi, CallingConvention=CallingConvention.Cdecl)>]
extern ErrorCode gp_widget_set_info(nativeint widget, [<MarshalAs(UnmanagedType.LPStr)>] string info)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_get_id(nativeint widget, int& id)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_get_readonly(nativeint widget, int& ro)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_set_readonly(nativeint widget, int ro)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_get_parent(nativeint widget, nativeint& parent)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_get_root(nativeint widget, nativeint& root)

[<DllImport("gphoto2", CharSet=CharSet.Ansi, CallingConvention=CallingConvention.Cdecl)>]
extern ErrorCode gp_widget_add_choice(nativeint widget, [<MarshalAs(UnmanagedType.LPStr)>] string choice)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_count_choices(nativeint widget)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_get_choice(nativeint widget, int choice_number, nativeint& choice)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_append(nativeint widget, nativeint child)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_prepend(nativeint widget, nativeint child)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_count_children(nativeint widget)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_get_child(nativeint widget, int child_number, nativeint& child)

[<DllImport("gphoto2", CharSet=CharSet.Ansi, CallingConvention=CallingConvention.Cdecl)>]
extern ErrorCode gp_widget_get_child_by_label(nativeint widget, [<MarshalAs(UnmanagedType.LPStr)>] string label, nativeint& child)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_get_child_by_id(nativeint widget, int id, nativeint& child)

[<DllImport("gphoto2", CharSet=CharSet.Ansi, CallingConvention=CallingConvention.Cdecl)>]
extern ErrorCode gp_widget_get_child_by_name(nativeint widget, [<MarshalAs(UnmanagedType.LPStr)>] string name, nativeint& child)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_get_range(nativeint range, float32& min, float32& max, float32& increment)

[<DllImport("gphoto2")>]
extern ErrorCode gp_widget_set_range(nativeint range, float32 min, float32 max, float32 increment)

[<DllImport("gphoto2", EntryPoint="gp_widget_get_value")>]
extern ErrorCode gp_widget_get_value_float(nativeint widget, float32& value)

[<DllImport("gphoto2", EntryPoint="gp_widget_set_value")>]
extern ErrorCode gp_widget_set_value_float(nativeint widget, float32 value)
