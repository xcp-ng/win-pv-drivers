using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

// https://learn.microsoft.com/en-us/windows-hardware/test/hlk/user/hlk-signing-with-an-hsm#packagedigitalsignaturemanager
static void Sign(string package, X509Certificate2 certificate) {
    // Open the package to sign it
    var packageToSign = Package.Open(package, FileMode.Open);

    try {
        // Specify that the digital signature should exist 
        // embedded in the signature part
        var signatureManager = new PackageDigitalSignatureManager(packageToSign) {
            CertificateOption = CertificateEmbeddingOption.InCertificatePart
        };

        // We want to sign every part in the package
        var partsToSign = packageToSign.GetParts().Select(x => x.Uri).ToList();

        // We will sign every relationship by type
        // This will mean the signature is invalidated if *anything* is modified in
        // the package post-signing
        var relationshipSelectors = packageToSign
            .GetRelationships()
            .Select(x => new PackageRelationshipSelector(
                x.SourceUri,
                PackageRelationshipSelectorType.Type,
                x.RelationshipType))
            .ToList();

        signatureManager.Sign(partsToSign, certificate, relationshipSelectors);
    } finally {
        packageToSign.Close();
    }
}

// This program doesn't work when written in PowerShell, but it does when written in C# (!?).
string? packagePath = null, certThumbprint = null;
var machine = false;
var storeName = "My";

try {
    for (int i = 0; i < args.Length; i++) {
        var arg = args[i];

        if ("-machine".Equals(arg, StringComparison.OrdinalIgnoreCase)) {
            machine = true;
        } else if ("-store".Equals(arg, StringComparison.OrdinalIgnoreCase)) {
            storeName = args[++i];
        } else if (packagePath == null) {
            packagePath = arg;
        } else if (certThumbprint == null) {
            certThumbprint = arg;
        } else {
            throw new ArgumentException($"Couldn't parse argument {i}: {arg}");
        }
    }

    if (packagePath == null || certThumbprint == null) {
        throw new ArgumentException("Required arguments not specified");
    }
} catch (Exception ex) {
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Usage: HlkxSigner [-machine] [-store <store>] <path> <thumbprint>");
    return 2;
}

try {
    using var store = new X509Store(storeName, machine ? StoreLocation.LocalMachine : StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);

    var signer = store.Certificates
        .Cast<X509Certificate2>()
        .Where(x => x.HasPrivateKey && x.Thumbprint.Equals(certThumbprint, StringComparison.OrdinalIgnoreCase))
        .FirstOrDefault()
        ?? throw new FileNotFoundException("Certificate not found");
    Sign(packagePath, signer);
} catch (Exception ex) {
    Console.Error.WriteLine(ex.Message);
    return 1;
}

return 0;
