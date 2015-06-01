<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet 
  version="1.0" 
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:msxsl="urn:schemas-microsoft-com:xslt"
  xmlns:maml="http://schemas.microsoft.com/maml/2004/10"
  exclude-result-prefixes="msxsl">
  
  <!-- WrapBareText.xsl
  
       (c) PoshBuild Contributors
       Released under the Microsoft Public License (Ms-PL)
       
       Processes a compiler-generated .xml help file (XmlDoc file) that has previously been transformed by
       XmlDocToMaml.xsl.
  -->
  
  <xsl:output method="xml"/>
  
  <xsl:template match="@* | node()">
    <xsl:copy>
      <xsl:apply-templates select="@* | node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Wrap any non-whitespace text nodes, not already inside a maml-ish element, inside <maml:para>.
       We assume that any element not in the default namespace is a maml-ish element. -->
  <xsl:template match="text()[not(namespace-uri(parent::*))]">
    <maml:para>
      <xsl:value-of select="."/>
    </maml:para>
  </xsl:template>
  
</xsl:stylesheet>
