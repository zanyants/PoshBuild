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
    <xsl:variable name="use_cref">
      <xsl:choose>
        <xsl:when test="@cref">
          <xsl:value-of select="@cref"/>  
        </xsl:when>
        <xsl:when test="ancestor::psoverride[last()][@cref]">
          <xsl:value-of select="ancestor::psoverride[last()]/@cref"/>
        </xsl:when>
        <xsl:otherwise>
          <xsl:message terminate="yes">psinclude elements must have an explicit or impliable cref attribute.</xsl:message>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:variable>
    <xsl:variable name="use_tag">
      <xsl:choose>
        <xsl:when test="@tag">
          <xsl:value-of select="@tag"/>
        </xsl:when>
        <xsl:when test="ancestor::psoverride">
          <xsl:value-of select="name( ancestor::*[parent::psoverride][last()] )"/>
        </xsl:when>
      </xsl:choose>
    </xsl:variable>
    <xsl:variable name="use_select">
      <xsl:choose>
        <xsl:when test="@select">
          <xsl:value-of select="@select"/>
        </xsl:when>
        <xsl:when test="$use_tag and @span">
          <xsl:value-of select="$use_tag"/>/span[@name='<xsl:value-of select="@span"/>']/node()
        </xsl:when>
        <xsl:when test="$use_tag">
          <xsl:value-of select="$use_tag"/>/node()
        </xsl:when>
        <xsl:otherwise>
          <xsl:message terminate="yes">psinclude elements must have an explicit or impliable select attribute.</xsl:message>    
        </xsl:otherwise>
      </xsl:choose>
    </xsl:variable>
    <xsl:choose>
      <xsl:when test="function-available('poshbuild:PsInclude')">
        <xsl:text xml:space="preserve"> </xsl:text>
        <xsl:apply-templates select="poshbuild:PsInclude( $use_cref, $use_select )"/>
      </xsl:when>
      <xsl:otherwise>
        [select '<xsl:value-of select="$use_select"/>' from <xsl:value-of select="$use_cref"/>]
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>
  
</xsl:stylesheet>
