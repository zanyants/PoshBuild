﻿<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet 
  version="1.0" 
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:msxsl="urn:schemas-microsoft-com:xslt"
  exclude-result-prefixes="msxsl">
  
  <!-- NormalizeWhitespace.xsl
  
       (c) PoshBuild Contributors
       Released under the Microsoft Public License (Ms-PL)
  -->
  
  <xsl:output method="xml"/>
  
  <xsl:template match="@* | node()">
    <xsl:copy>
      <xsl:apply-templates select="@* | node()"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="text()[not(parent::*[@xml:space = 'preserve'])]">
    <xsl:value-of select="normalize-space()"/>
  </xsl:template>
  
</xsl:stylesheet>
