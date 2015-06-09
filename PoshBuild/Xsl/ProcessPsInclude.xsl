<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet 
  version="1.0" 
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:msxsl="urn:schemas-microsoft-com:xslt"
  xmlns:poshbuild="urn:poshbuild"
  exclude-result-prefixes="msxsl poshbuild">
  
  <!-- ProcessPsInclude.xsl
  
       (c) PoshBuild Contributors
       Released under the Microsoft Public License (Ms-PL)
       
       Processes a compiler-generated XmlDoc file, processing psinclude elements.
  -->
  
  <xsl:output method="xml"/>
  
  <xsl:template match="@* | node()">
    <xsl:copy>
      <xsl:apply-templates select="@* | node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="psinclude">
    <xsl:if test="not(@cref)">
      <xsl:message terminate="yes">psinclude elements must have a cref attribute.</xsl:message>
    </xsl:if>
    <xsl:if test="not(@select)">
      <xsl:message terminate="yes">psinclude elements must have a select attribute.</xsl:message>
    </xsl:if>
    <xsl:choose>
      <xsl:when test="function-available('poshbuild:PsInclude')">
        <xsl:text xml:space="preserve"> </xsl:text>
        <xsl:apply-templates select="poshbuild:PsInclude( @cref, @select )"/>
      </xsl:when>
      <xsl:otherwise>
        [select '<xsl:value-of select="@select"/>' from <xsl:value-of select="@cref"/>]
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>
  
</xsl:stylesheet>
