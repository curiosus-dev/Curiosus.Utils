using System;

namespace Curiosus.SMS
{
    /// <summary>
    /// Masks phone numbers for logs.
    /// </summary>
    public static class PhoneNumberMask
    {
        /// <summary>
        /// Keeps the first two and the last four characters: <c>+79001234567</c> → <c>+7******4567</c>. Numbers shorter
        /// than seven characters keep only the last two.
        /// </summary>
        public static string Mask(string? phoneNumber)
        {
            if (String.IsNullOrEmpty(phoneNumber)) return String.Empty;

            if (phoneNumber.Length < 7)
                return new string('*', Math.Max(0, phoneNumber.Length - 2)) + phoneNumber.Substring(Math.Max(0, phoneNumber.Length - 2));

            return phoneNumber.Substring(0, 2) + new string('*', phoneNumber.Length - 6) + phoneNumber.Substring(phoneNumber.Length - 4);
        }
    }
}
