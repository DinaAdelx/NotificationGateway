namespace NotificationGateway.Application.Abstractions;

public interface ICredentialEncryptor
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
