using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class DeviceRulesTests
{
    [Fact]
    public void Pnp_classes_map_to_display_groups()
    {
        Assert.Equal("Display", DeviceRules.Group("Display", 0));
        Assert.Equal("USB", DeviceRules.Group("USB", 0));
        Assert.Equal("USB", DeviceRules.Group("USBDevice", 0));
        Assert.Equal("Input", DeviceRules.Group("HIDClass", 0));
        Assert.Equal("Input", DeviceRules.Group("keyboard", 0));
        Assert.Equal("Input", DeviceRules.Group("Mouse", 0));
        Assert.Equal("Network", DeviceRules.Group("Net", 0));
        Assert.Equal("Audio", DeviceRules.Group("MEDIA", 0));
        Assert.Equal("Storage", DeviceRules.Group("SCSIAdapter", 0));
        Assert.Equal("Storage", DeviceRules.Group("HDC", 0));
        Assert.Equal("System", DeviceRules.Group("System", 0));
        Assert.Equal("Other", DeviceRules.Group("Monitor", 0));
        Assert.Equal("Other", DeviceRules.Group(null, 0));
        Assert.Equal(new[] { "Display", "Processor", "USB", "Input", "Network", "Audio", "Bluetooth", "Storage", "System", "Other" },
            DeviceRules.GroupOrder);
    }

    [Fact]
    public void Noise_classes_are_hidden_unless_the_device_has_a_problem()
    {
        Assert.Null(DeviceRules.Group("SoftwareDevice", 0));
        Assert.Null(DeviceRules.Group("AudioEndpoint", 0));
        Assert.Null(DeviceRules.Group("DiskDrive", 0));
        Assert.Null(DeviceRules.Group("Volume", 22));
        Assert.Equal("Storage", DeviceRules.Group("DiskDrive", 43));
        Assert.Equal("Audio", DeviceRules.Group("AudioEndpoint", 10));
        Assert.Equal("Other", DeviceRules.Group("SoftwareDevice", 28));
    }

    [Fact]
    public void Problem_codes_read_as_short_text()
    {
        Assert.False(DeviceRules.IsProblem(0));
        Assert.False(DeviceRules.IsProblem(22));
        Assert.True(DeviceRules.IsDisabled(22));
        Assert.True(DeviceRules.IsProblem(43));
        Assert.Equal("Windows has stopped this device", DeviceRules.ProblemText(43));
        Assert.Equal("Drivers not installed", DeviceRules.ProblemText(28));
        Assert.Equal("Disabled", DeviceRules.ProblemText(22));
        Assert.Equal("Problem", DeviceRules.ProblemText(99));

        Assert.Equal("Realtek", DeviceFormat.Description("Realtek", 0));
        Assert.Equal("Code 43 · Windows has stopped this device", DeviceFormat.Description("", 43));
        Assert.Equal("Microsoft · Code 22 · Disabled", DeviceFormat.Description(" Microsoft ", 22));
        Assert.Equal("USB (4)", DeviceFormat.GroupTitle("USB", 4));
        Assert.Equal("NETWORK (1)", DeviceFormat.GroupTitle("Network", 1));
    }

    [Fact]
    public void Power_instance_names_map_back_to_device_ids()
    {
        Assert.Equal(@"USB\VID_046D&PID_C547\6D8E385C3031", DeviceRules.InstanceIdOf(@"USB\VID_046D&PID_C547\6D8E385C3031_0"));
        Assert.Equal(@"HID\VID_046D&PID_C547&MI_01&COL01\8&2A1&0&0000", DeviceRules.InstanceIdOf(@"HID\VID_046D&PID_C547&MI_01&COL01\8&2A1&0&0000_0"));
        Assert.Equal(@"ACPI\PNP0103\0", DeviceRules.InstanceIdOf(@"ACPI\PNP0103\0"));
        Assert.True(DeviceRules.IsUsbOrInput("USB"));
        Assert.True(DeviceRules.IsUsbOrInput("Input"));
        Assert.False(DeviceRules.IsUsbOrInput("Network"));
    }
}
