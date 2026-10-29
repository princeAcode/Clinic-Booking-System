namespace ClinicBooking.Client.Components;

/// <summary>Consistent date and time formatting across pages. All times are clinic-local.</summary>
public static class DisplayFormat
{
    public static string Date(DateTime value) => value.ToString("ddd d MMM yyyy");

    public static string Time(DateTime value) => value.ToString("HH:mm");

    public static string TimeRange(DateTime start, DateTime end) => $"{Time(start)} – {Time(end)}";
}
