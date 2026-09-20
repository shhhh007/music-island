function Get-ReleaseCertificate([string]$Thumbprint) {
    if($Thumbprint -notmatch '^[0-9a-fA-F]{40}$'){throw 'Specify a code-signing certificate thumbprint (40 hexadecimal characters).'}
    $certificate=Get-Item -LiteralPath "Cert:\CurrentUser\My\$Thumbprint" -ErrorAction Stop
    if(-not $certificate.HasPrivateKey){throw 'The signing certificate has no accessible private key.'}
    if($certificate.NotBefore -gt (Get-Date) -or $certificate.NotAfter -le (Get-Date)){throw 'The signing certificate is outside its validity period.'}
    if($certificate.PublicKey.Oid.Value -ne '1.2.840.113549.1.1.1'){throw 'An RSA code-signing certificate is required for Smart App Control compatibility.'}
    $chain=[Security.Cryptography.X509Certificates.X509Chain]::new()
    try {
        $chain.ChainPolicy.ApplicationPolicy.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
        $chain.ChainPolicy.RevocationMode='Online'
        $chain.ChainPolicy.UrlRetrievalTimeout=[TimeSpan]::FromSeconds(15)
        if(-not $chain.Build($certificate)){throw 'Windows could not verify the signing certificate chain and revocation status.'}
    } finally {$chain.Dispose()}
    return $certificate
}
function Set-ReleaseSignature([string[]]$Paths,$Certificate,[string]$TimestampServer) {
    if(-not $TimestampServer.StartsWith('http://',[StringComparison]::OrdinalIgnoreCase)){throw 'Windows PowerShell Authenticode timestamping requires an http:// timestamp endpoint from your signing provider.'}
    foreach($path in $Paths){
        $signature=Set-AuthenticodeSignature -LiteralPath $path -Certificate $Certificate -HashAlgorithm SHA256 -IncludeChain NotRoot -TimestampServer $TimestampServer -ErrorAction Stop
        if($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate){throw "Signing or timestamp verification failed: $path"}
        $verified=Get-AuthenticodeSignature -LiteralPath $path
        if($verified.Status -ne 'Valid' -or $verified.SignerCertificate.Thumbprint -ne $Certificate.Thumbprint){throw "Signature verification failed: $path"}
    }
}
