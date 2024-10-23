<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet
  version="1.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns="http://wixtoolset.org/schemas/v4/wxs"
  xmlns:wix="http://wixtoolset.org/schemas/v4/wxs">
  <xsl:output method="xml" version="1.0" encoding="UTF-8" indent="yes"/>
  <xsl:strip-space elements="*"/>

  <!--
    Why the XSLT horror?
    Firstly, because the driver input comes from a cabinet packaged by Microsoft, where we don't control its contents.
    Secondly, Wix 5 <Files> doesn't work since it doesn't have an option for default versioning all files,
    nor does it work to specify companion files.
    If any unversioned file is modified by the user, it will become stuck and mess with upgrades
    (https://learn.microsoft.com/en-us/windows/win32/msi/file-versioning-rules)
  -->

  <xsl:template match="wix:Wix">
    <xsl:copy>
      <xsl:processing-instruction name="include">$(ProjectDir)/Include.wxi</xsl:processing-instruction>
      <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="wix:File">
    <xsl:choose>
      <xsl:when test="contains(@Source, '.inf')">
        <!--
          replace the entire thing with a dummy component
          so that Wix doesn't complain when the component's KeyPath File element is removed
        -->
        <RegistryValue Root="HKLM" Key="$(VENDOR_NAME)\!(loc.GenericProductName)">
          <xsl:attribute name="Name">
            <xsl:value-of select="@Id" />
          </xsl:attribute>
          <xsl:attribute name="Value">
            <xsl:value-of select="@Source" />
          </xsl:attribute>
        </RegistryValue>
      </xsl:when>

      <xsl:otherwise>
        <xsl:copy>
          <xsl:attribute name="DefaultVersion">$(ProductVersion)</xsl:attribute>
          <xsl:attribute name="DefaultLanguage">1033</xsl:attribute>
          <xsl:apply-templates select="@*|node()"/>
        </xsl:copy>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <!-- copy everything else as-is -->
  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>
