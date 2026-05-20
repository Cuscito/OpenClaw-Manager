using System;

namespace OpenClawManager;

public class LicenseData
{
    public string Email { get; set; } = "";
    public string MachineHash { get; set; } = "";
    public string Level { get; set; } = "pro";
    public DateTime Expiry { get; set; }
    public DateTime Issued { get; set; }
    // 绑定类型: "machine"=绑电脑, "usb"=绑U盘(默认machine兼容旧许可)
    public string BindingType { get; set; } = "machine";

    public bool IsExpired => DateTime.Now > Expiry;
}
