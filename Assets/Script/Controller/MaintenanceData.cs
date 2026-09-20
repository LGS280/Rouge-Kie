using System;

/// <summary>
/// Model phản hồi trạng thái bảo trì hệ thống từ API /api/maintenance/current
/// </summary>
[Serializable]
public class CurrentMaintenanceStatus
{
    public bool isUnderMaintenance;
    public string title = "";
    public string message = "";
    public string startTime;
    public string endTime;
    public int remainingMinutes;
}

/// <summary>
/// Model dữ liệu phản hồi chung từ API khi có thông tin bảo trì (HTTP 503)
/// </summary>
[Serializable]
public class MaintenanceApiResponse
{
    public bool success;
    public string message = "";
    public bool isMaintenance;
    public CurrentMaintenanceStatus maintenance;
}
