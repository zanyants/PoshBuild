<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet 
  version="1.0" 
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:msxsl="urn:schemas-microsoft-com:xslt"
  xmlns:msh="http://msh"
  xmlns:maml="http://schemas.microsoft.com/maml/2004/10"
  xmlns:command="http://schemas.microsoft.com/maml/dev/command/2004/10"
  xmlns:dev="http://schemas.microsoft.com/maml/dev/2004/10"
  xmlns:poshbuild="urn:poshbuild"
  exclude-result-prefixes="msxsl poshbuild">
  
  <!-- XmlDocToMaml.xsl
  
       (c) PoshBuild Contributors
       Released under the Microsoft Public License (Ms-PL)
  -->
  
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
  
  <!-- Promote psoverride elements to children of /doc/members, check and add @context -->
  <xsl:template match="/doc/members">
    <members>
      <xsl:apply-templates select="member/psoverride" mode="members"/>
      <xsl:apply-templates select="member" />
    </members>
  </xsl:template>

  <xsl:template match="psoverride" mode="members">
    <xsl:variable name="declaringId" select="ancestor::member/@name" />
    <xsl:if test="not( starts-with( $declaringId, 'T:' ) )">
      <xsl:message terminate="yes">psoverride elements may only be delcared at type scope.</xsl:message>
    </xsl:if>
    <xsl:if test="not( @cref )">
      <xsl:message terminate="yes">psoverride elements must have a cref attribute.</xsl:message>
    </xsl:if>
    <psoverride>
      <xsl:attribute name="context">
        <xsl:value-of select="$declaringId"/>
      </xsl:attribute>
      <xsl:attribute name="cref">
        <xsl:value-of select="@cref" />
      </xsl:attribute>
      <xsl:apply-templates select="node()" mode="member" />
    </psoverride>
  </xsl:template>

    <!-- We're only interested in nodes within member elements. -->
  <xsl:template match="/doc/members/member">
    <xsl:copy>
      <xsl:apply-templates select="@*"/>
      <xsl:apply-templates select="node()" mode="member"/>
      <!-- Copy remarks/psnote elements to this level. -->
      <xsl:apply-templates select="remarks/psnote" mode="member"/>
    </xsl:copy>
  </xsl:template>

  <!-- Special case for psexample elements. -->
  <xsl:template match="example/psexample" mode="memberContent">
    <xsl:if test="not(*[1][name()='code'])">
      <xsl:message terminate="yes">psexample elements must have a code element as the first child (see example element for member <xsl:value-of select="../../@name"/>)</xsl:message>
    </xsl:if>
<command:example>
<maml:title xml:space="preserve">
      
-------------------------- EXAMPLE <xsl:value-of select="count(preceding-sibling::psexample) + 1"/> --------------------------     

</maml:title>
<maml:introduction>
  <maml:para>PS C:\&gt; </maml:para>
</maml:introduction>
      <xsl:apply-templates select="@* | node()" mode="memberContent"/>
</command:example>
  </xsl:template>

  <!-- Transform first psexample/code to dev:code -->
  <xsl:template match="example/psexample/code[1]" mode="memberContent">
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

  <!-- Related links -->
  <xsl:template match="psrelated" mode="member">
    <psrelated>
      <maml:navigationLink>
        <maml:linkText>
          <xsl:apply-templates select="node()" mode="memberContent" />
        </maml:linkText>
        <maml:uri>
          <xsl:value-of select="@href"/>
        </maml:uri>
      </maml:navigationLink>
    </psrelated>
  </xsl:template>
  
  <!-- Check structure and move psnote elements from remarks section to be children of member. -->
  <xsl:template match="remarks/psnote" mode="member">
    <xsl:if test="count(title) > 1">
      <xsl:message terminate="yes">psnote elements may contain at most one title element (see remarks element for member <xsl:value-of select="../../@name"/>)</xsl:message>
    </xsl:if>
    <xsl:if test="count(title) = 1 and count( title/preceding-sibling::* | title/preceding-sibling::text()[not( normalize-space() = '' or normalize-space() = ' ' )] ) != 0">
      <xsl:message terminate="yes">The title element must be the first child of psnote, and may not be preceded by text (see remarks element for member <xsl:value-of select="../../@name"/>)</xsl:message>
    </xsl:if>
    <psnote>
      <maml:title>
        <xsl:value-of select="normalize-space(title)"/>
      </maml:title>
      <maml:alert>
        <xsl:apply-templates select="node()" mode="memberContent"/>
      </maml:alert>
    </psnote>    
  </xsl:template>

  <!-- Skip psnote/title - it's handled directly in the template above. -->
  <xsl:template match="psnote/title" mode="memberContent" />

  <!-- Remove psnote elements from remarks section. They are copied up a level. -->
  <xsl:template match="remarks/psnote" mode="memberContent" />

  <!-- Remove psoverride elements. They are copied up a level. -->
  <xsl:template match="psoverride" mode="member" />

  <!-- Standard @name containing elements - replace with content of @name -->
  <xsl:template match="paramref | typenameref" mode="memberContent">
    <xsl:value-of select="@name"/>
  </xsl:template>
  
  <!-- see - replace with prettified content of @cref -->
  <xsl:template match="see" mode="memberContent">
    <xsl:variable name="scopeRoot" select="( ancestor::*[name()='psnote' or name()='psexample'] | ancestor::*[parent::member] )[last()]"/>
    <xsl:variable name="allPrecedingSee" select="preceding::see" />
    <xsl:variable name="rootPrecedingSee" select="$scopeRoot/preceding::see" />
    <!-- Compute the set difference between the $allPrecedingSee and $rootPrecedingSee nodesets: -->
    <xsl:variable name="scopeSee" select="$allPrecedingSee[count(. | $rootPrecedingSee) != count($rootPrecedingSee)]" />
    <xsl:variable name="scopeCref" select="$scopeSee/@cref" />
    <xsl:choose>
      <xsl:when test="function-available('poshbuild:GetPrettyNameForIdentifier')">
        <xsl:value-of select="poshbuild:GetPrettyNameForIdentifier( ancestor::member/@name, @cref, @psstyle, $scopeCref )"/>
      </xsl:when>
      <xsl:otherwise>
        <xsl:value-of select="@cref"/>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <!-- seealso - remove (these are normally rendered into a separate "See Also" section) -->
  <xsl:template match="seealso" mode="memberContent" />

    <!-- bullet-type list -->
  <xsl:template match="list[@type='bullet']" mode="memberContent">
    <xsl:apply-templates select="node()" mode="bulletList"/>    
  </xsl:template>
  
  <xsl:template match="item[term and description]" mode="bulletList">
    <maml:para>-- <xsl:apply-templates select="term/node()" mode="memberContent" />: <xsl:apply-templates select="description/node()" mode="memberContent" /></maml:para>
  </xsl:template>

  <xsl:template match="item[not(term and description)]" mode="bulletList">
    <maml:para>-- <xsl:apply-templates select="node()" mode="memberContent" /></maml:para>
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
    <maml:para><xsl:value-of select="$numberListStart + count(preceding-sibling::item)"/>: <xsl:apply-templates select="term/node()" mode="memberContent" /> (<xsl:apply-templates select="description/node()" mode="memberContent" />)</maml:para>
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
    <maml:para><xsl:value-of select="$numberListStart + count(preceding-sibling::item)"/>: <xsl:apply-templates select="node()" mode="memberContent" /></maml:para>
  </xsl:template>

  <!-- definition-type list -->
  <xsl:template match="list[@type='definition']" mode="memberContent">
    <xsl:apply-templates select="node()" mode="definitionList"/>    
  </xsl:template>
  
  <xsl:template match="item[term and description]" mode="definitionList">
    <maml:para><xsl:apply-templates select="term/node()" mode="memberContent" />:</maml:para>
    <maml:para>-- <xsl:apply-templates select="description/node()" mode="memberContent" /></maml:para>
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
    <xsl:if test="count(preceding-sibling::term) > 0">, </xsl:if><xsl:apply-templates select="node()" mode="memberContent" />
  </xsl:template>
  
  <xsl:template match="description" mode="tableListRow">
    <xsl:if test="count(preceding-sibling::description) > 0">, </xsl:if><xsl:apply-templates select="node()" mode="memberContent" />
  </xsl:template>

  <!-- table row *is* of term-defintion style -->
  <xsl:template match="listheader[ count(term) = 1 and count(description) = 1 ] | item[ count(term) = 1 and count(description) = 1 ]" mode="tableList">
    <maml:para><xsl:apply-templates select="term/node()" mode="memberContent" />, <xsl:apply-templates select="description/node()" mode="memberContent" /></maml:para>
  </xsl:template>
  
  <!-- For any other elements within member/* elements, replace with inner text (eg, <c>, <see>) -->
  <xsl:template match="*" mode="memberContent">
    <xsl:value-of select="normalize-space()"/>
  </xsl:template>
  
  <!-- For any member/* elements without special-case handling (currently only psexample has special case), copy
       all attributes, process content in memberContent mode. -->
  <xsl:template match="*" mode="member">
    <xsl:copy>
      <xsl:apply-templates select="@*" />
      <xsl:apply-templates select="node()" mode="memberContent"/>
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
