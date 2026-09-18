using System.Text;

namespace ReservationService.Services;

public static class EnumExtensions
{
    public static string ToWireString(this Enum value)
    {
        var name = value.ToString();
        var builder = new StringBuilder();

        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                builder.Append('_');
            }
            builder.Append(char.ToUpperInvariant(name[i]));
        }

        return builder.ToString();
    }
}