using DriverPlatform.Infrastructure.Security;

namespace DriverPlatform.Tests.Auth;

public sealed class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verify_accepts_the_original_password()
    {
        var hash = _hasher.Hash("s3cret-pass");

        Assert.True(_hasher.Verify("s3cret-pass", hash));
        Assert.False(_hasher.Verify("other-pass", hash));
    }

    [Fact]
    public void Hash_uses_a_new_salt_each_time()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("pbkdf2$abc$salt$hash")]
    public void Verify_rejects_malformed_hashes(string hash)
    {
        Assert.False(_hasher.Verify("anything", hash));
    }
}
