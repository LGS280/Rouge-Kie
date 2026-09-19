using System;

/// <summary>
/// Model nhận trạng thái bảo trì hệ thống từ Backend API (/api/maintenance/current)
/// </summary>
[System.Serializable]
public class MaintenanceStatus
{
    public bool isUnderMaintenance;
    public string title;
    public string message;
    public string startTime;
    public string endTime;
    public int remainingMinutes;
}
