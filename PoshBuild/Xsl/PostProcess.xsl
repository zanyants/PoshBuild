<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet 
  version="1.0" 
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:msxsl="urn:schemas-microsoft-com:xslt"
  xmlns:maml="http://schemas.microsoft.com/maml/2004/10"
  xmlns:command="http://schemas.microsoft.com/maml/dev/command/2004/10"  
  exclude-result-prefixes="msxsl">
  
  <!-- PostProcess.xsl
  
       (c) PoshBuild Contributors
       Released under the Microsoft Public License (Ms-PL)
       
       Post-process the generated MAML help file. Expects a document with <msh:helpItems> root element.
  -->
  
  <xsl:output method="xml"/>
  
  <xsl:template match="@* | node()">
    <xsl:copy>
      <xsl:apply-templates select="@* | node()"/>
    </xsl:copy>
  </xsl:template>
  
  <!-- Strip any empty sections as these show up as empty headings in ps console. -->
  <xsl:template match="command:command/*[not(*)]" />
  
</xsl:stylesheet>
