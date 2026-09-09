namespace SysPitstops.Api.Domain;

// How a vehicle is written wherever a screen names one. It lives here because
// the board, the order detail and the quote list all have to spell it the same
// way: the customer reads this same string in the WhatsApp message.
public static class VehicleLabel
{
    public static string Describe(string brand, string model, int? modelYear) =>
        modelYear is null ? $"{brand} {model}" : $"{brand} {model} {modelYear}";
}
