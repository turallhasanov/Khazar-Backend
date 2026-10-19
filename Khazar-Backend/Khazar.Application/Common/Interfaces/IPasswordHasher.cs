namespace Khazar.Application.Common.Interfaces;

public interface IPasswordHasher
{
    /// <summary>Returns a self-describing hash (algorithm parameters and salt included) of the password.</summary>
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
