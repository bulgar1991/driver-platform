namespace DriverPlatform.Domain.Enums;

public static class PaymentMethods
{
    /// <summary>Paid outside Stripe: the office records it (no refund through Stripe).</summary>
    public static bool IsManual(this PaymentMethod method) => method is not (PaymentMethod.Card or PaymentMethod.PayPal);

    /// <summary>The driver collects it at the ride.</summary>
    public static bool IsOnBoard(this PaymentMethod method) => method is PaymentMethod.CashOnBoard or PaymentMethod.CardOnBoard;
}
