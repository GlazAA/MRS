using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace MRS.Application.Facilities;

/// <summary>Проверка и форматирование телефона/email контакта заказчика.</summary>
public static partial class ContactInputRules
{
	/// <summary>Российский номер: 11 цифр, начинается с 7. Пустое значение допустимо.</summary>
	public static bool IsPhoneValidOrEmpty(string? phone)
	{
		if (string.IsNullOrWhiteSpace(phone))
			return true;

		var digits = DigitsOnly(phone);
		return digits.Length == 11 && digits[0] == '7';
	}

	public static string? PhoneValidationHint(string? phone)
	{
		if (string.IsNullOrWhiteSpace(phone))
			return null;
		if (IsPhoneValidOrEmpty(phone))
			return null;

		var digits = DigitsOnly(phone);
		if (digits.Length == 0)
			return "Введите номер, например +7 (999) 123-45-67.";
		if (digits.Length < 11)
			return $"Не хватает цифр: сейчас {digits.Length} из 11 (формат +7 XXX XXX-XX-XX).";
		if (digits.Length > 11)
			return "Слишком много цифр: нужно 11, начиная с +7.";
		return "Номер должен начинаться с +7 (российский мобильный/городской).";
	}

	/// <summary>
	/// Нормализует ввод к виду +7 (XXX) XXX-XX-XX.
	/// 8XXXXXXXXXX → +7…; 10 цифр без кода страны → +7…
	/// </summary>
	public static string FormatPhoneInput(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return string.Empty;

		var digits = DigitsOnly(raw);
		if (digits.Length == 0)
			return string.Empty;

		if (digits[0] == '8')
			digits = "7" + digits[1..];
		else if (digits.Length == 10 && digits[0] != '7')
			digits = "7" + digits;
		else if (digits[0] != '7')
			digits = "7" + digits;

		if (digits.Length > 11)
			digits = digits[..11];

		var sb = new StringBuilder("+7");
		if (digits.Length <= 1)
			return sb.ToString();

		sb.Append(" (");
		sb.Append(digits.AsSpan(1, Math.Min(3, digits.Length - 1)));
		if (digits.Length <= 4)
			return sb.ToString();

		sb.Append(") ");
		sb.Append(digits.AsSpan(4, Math.Min(3, digits.Length - 4)));
		if (digits.Length <= 7)
			return sb.ToString();

		sb.Append('-');
		sb.Append(digits.AsSpan(7, Math.Min(2, digits.Length - 7)));
		if (digits.Length <= 9)
			return sb.ToString();

		sb.Append('-');
		sb.Append(digits.AsSpan(9, Math.Min(2, digits.Length - 9)));
		return sb.ToString();
	}

	/// <summary>Базовая проверка email; домен не ограничиваем (yandex, mail.ru, gmail и т.д.). Пусто — ок.</summary>
	public static bool IsEmailValidOrEmpty(string? email)
	{
		if (string.IsNullOrWhiteSpace(email))
			return true;

		var value = email.Trim();
		if (value.Contains(' ', StringComparison.Ordinal) || value.Count(c => c == '@') != 1)
			return false;

		try
		{
			var addr = new MailAddress(value);
			if (!string.Equals(addr.Address, value, StringComparison.OrdinalIgnoreCase))
				return false;

			var at = value.IndexOf('@');
			var domain = value[(at + 1)..];
			return domain.Contains('.', StringComparison.Ordinal)
			       && !domain.StartsWith('.')
			       && !domain.EndsWith('.')
			       && BasicEmailRegex().IsMatch(value);
		}
		catch (FormatException)
		{
			return false;
		}
	}

	public static string? EmailValidationHint(string? email)
	{
		if (string.IsNullOrWhiteSpace(email))
			return null;
		if (IsEmailValidOrEmpty(email))
			return null;

		return "Проверьте адрес: имя@домен.зона (например name@yandex.ru, name@mail.ru).";
	}

	private static string DigitsOnly(string phone) =>
		new string(phone.Where(char.IsDigit).ToArray());

	[GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex BasicEmailRegex();
}
