<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet 
  version="1.0" 
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:msxsl="urn:schemas-microsoft-com:xslt"
  xmlns:msh="http://msh"
  xmlns:maml="http://schemas.microsoft.com/maml/2004/10"
  xmlns:command="http://schemas.microsoft.com/maml/dev/command/2004/10"
  xmlns:dev="http://schemas.microsoft.com/maml/dev/2004/10"
  exclude-result-prefixes="msxsl">
  
  <xsl:output 
    method="xml" 
    indent="yes" />
  
  <xsl:template match="@* | node()">
    <xsl:copy>
      <xsl:apply-templates select="@* | node()"/>
    </xsl:copy>
  </xsl:template>

  <!-- Add maml namespaces to root node, keeps rest of output doc clean. -->
  <xsl:template match="/doc">
    <doc
      xmlns:msh="http://msh"
      xmlns:maml="http://schemas.microsoft.com/maml/2004/10"
      xmlns:command="http://schemas.microsoft.com/maml/dev/command/2004/10"
      xmlns:dev="http://schemas.microsoft.com/maml/dev/2004/10">
      <xsl:apply-templates select="@* | node()"/>
    </doc>
  </xsl:template>
  
  <!-- We're only interested in nodes within member elements. -->
  <xsl:template match="/doc/members/member">
    <xsl:copy>
      <xsl:apply-templates select="@*"/>
      <xsl:apply-templates select="node()" mode="member"/>
    </xsl:copy>
  </xsl:template>
  
  <!-- Special case for psexample elements. -->
  <xsl:template match="psexample" mode="member">
    <xsl:if test="not(*[1][name()='code'])">
      <xsl:message terminate="yes">psexample elements must have a code element as the first child.</xsl:message>
    </xsl:if>
    <xsl:copy>
<maml:title>
      
-------------------------- EXAMPLE <xsl:value-of select="count(preceding-sibling::psexample) + 1"/> --------------------------     

</maml:title>
<maml:introduction>
  <maml:para>PS C:\&gt; </maml:para>
</maml:introduction>
      <xsl:apply-templates select="@* | node()" mode="memberContent"/>
    </xsl:copy>      
  </xsl:template>

  <!-- Transform first psexample/code to dev:code -->
  <xsl:template match="psexample/code[1]" mode="memberContent">
    <dev:code>
      <xsl:apply-templates select="@* | node()" mode="memberContent"/>
    </dev:code>
  </xsl:template>
  
  <!-- Transform all para to maml:para -->
  <xsl:template match="para" mode="memberContent">
    <maml:para>
      <xsl:apply-templates select="@* | node()" mode="memberContent"/>    
    </maml:para>
  </xsl:template>
  
  <!-- bullet-type list -->
  <xsl:template match="list[@type='bullet']" mode="memberContent">
    <xsl:apply-templates select="node()" mode="bulletList"/>    
  </xsl:template>
  
  <xsl:template match="item[term and description]" mode="bulletList">
    <maml:para>-- <xsl:value-of select="term"/>: <xsl:value-of select="description"/></maml:para>
  </xsl:template>

  <xsl:template match="item[not(term and description)]" mode="bulletList">
    <maml:para>-- <xsl:value-of select="."/></maml:para>
  </xsl:template>
  
  <!-- number-type list -->
  <xsl:template match="list[@type='number']" mode="memberContent">
    <xsl:apply-templates select="node()" mode="numberList"/>    
  </xsl:template>
  
  <xsl:template match="item[term and description]" mode="numberList">
    <xsl:variable name="numberListStart">
      <xsl:choose>
        <xsl:when test="../@start">
          <xsl:value-of select="../@start"/>
        </xsl:when>
        <xsl:otherwise>
          1
        </xsl:otherwise>
      </xsl:choose>
    </xsl:variable>
    <maml:para><xsl:value-of select="$numberListStart + count(preceding-sibling::item)"/>: <xsl:value-of select="term"/> (<xsl:value-of select="description"/>)</maml:para>
  </xsl:template>

  <xsl:template match="item[not(term and description)]" mode="numberList">
    <xsl:variable name="numberListStart">
      <xsl:choose>
        <xsl:when test="../@start">
          <xsl:value-of select="../@start"/>
        </xsl:when>
        <xsl:otherwise>
          1
        </xsl:otherwise>
      </xsl:choose>
    </xsl:variable>
    <maml:para><xsl:value-of select="$numberListStart + count(preceding-sibling::item)"/>: <xsl:value-of select="."/></maml:para>
  </xsl:template>

  <!-- definition-type list -->
  <xsl:template match="list[@type='definition']" mode="memberContent">
    <xsl:apply-templates select="node()" mode="definitionList"/>    
  </xsl:template>
  
  <xsl:template match="item[term and description]" mode="definitionList">
    <maml:para><xsl:value-of select="term"/>:</maml:para>
    <maml:para>-- <xsl:value-of select="description"/></maml:para>
    <maml:para/>
  </xsl:template>

  <xsl:template match="item[not(term and description)]" mode="definitionList">
    <xsl:message terminate="yes">Items within a definition list must contain term and description elements.</xsl:message>
  </xsl:template>

  <!-- table-type list: render each row as comma separated. -->
  <xsl:template match="list[@type='table']" mode="memberContent">
    <xsl:apply-templates select="node()" mode="tableList"/>    
  </xsl:template>

  <!-- table row not of term-defintion style -->
  <xsl:template match="listheader[not( count(term) = 1 and count(description) = 1 )] | item[not( count(term) = 1 and count(description) = 1 )]" mode="tableList">
    <maml:para>
      <xsl:apply-templates select="term | description" mode="tableListRow"/>
    </maml:para>
  </xsl:template>
  
  <xsl:template match="term" mode="tableListRow">
    <xsl:if test="count(preceding-sibling::term) > 0">, </xsl:if><xsl:value-of select="."/>
  </xsl:template>
  
  <xsl:template match="description" mode="tableListRow">
    <xsl:if test="count(preceding-sibling::description) > 0">, </xsl:if><xsl:value-of select="."/>
  </xsl:template>

  <!-- table row *is* of term-defintion style -->
  <xsl:template match="listheader[ count(term) = 1 and count(description) = 1 ] | item[ count(term) = 1 and count(description) = 1 ]" mode="tableList">
    <maml:para><xsl:value-of select="term"/>, <xsl:value-of select="description"/></maml:para>
  </xsl:template>
  
  <!-- For any other elements within member/* elements, replace with inner text (eg, <c>, <see>) -->
  <xsl:template match="*" mode="memberContent">
    <xsl:value-of select="string()"/>
  </xsl:template>
  
  <!-- For any member/* elements without special-case handling (currently only psexample has special case), copy
       all attributes, process content in memberContent mode. -->
  <xsl:template match="*" mode="member">
    <xsl:copy>
      <xsl:apply-templates select="@*" />
      <xsl:apply-templates select="node()" mode="memberContent"/>
    </xsl:copy>
  </xsl:template>

  <!-- Wrap any non-whitespace text nodes, not already inside a para or handled specially elsewhere, inside <maml:para> -->
  <xsl:template match="text()[not(parent::para) and not( normalize-space() = '' or normalize-space() = ' ' )]" mode="memberContent">
    <maml:para>
      <xsl:value-of select="."/>
    </maml:para>
  </xsl:template>

</xsl:stylesheet>
