# Contributing

This page describes how to contribute to [APIView](../../../src//dotnet/APIView/APIViewWeb/CONTRIBUTING.md) language level parsers.
Specifically how to create or update a language parser to produce tree style tokens for APIView.

## Creating Tree Style Tokens
The main idea is to capture the hierachy of the API using a tree data structure, then maintain a flat list of tokens for each node of the tree.

![APITree](APITree.svg)

Each tree node has top tokens which should be used to capture the main tokens on the node, these can span multiple lines. If the language requires it use the bottom tokens to capture tokens that closes out the node, this is usually just the closing bracket and/or empty lines.

- Here are the models needed
  ```
  object APITreeNode
    string Name
    string Id
    string Kind
    Set<string> Tags
    Dictionary<string, string> Properties
    List<StructuredToken> TopTokens
    List<StructuredToken> BottomTokens
    List<APITreeNode> Children

  object StructuredToken
    string Value
    string Id
    StructuredTokenKind Kind
    Set<string> Tags
    Dictionary<string, string> Properties 
    Set<string> RenderClasses 

  enum StructuredTokenKind
    Content
    LineBreak
    NoneBreakingSpace
    TabSpace
    ParameterSeparator
    Url
  ```
Each module of the API (namespace, class, method) should be its own node. Members of a module (methods, in a class), (classes in a namespace) should be added as child nodes of its parent module.

Sort each node at each level of the tree by your desired property, this is to ensure that difference in node order does not result in diff.

Ensure each node must have an Id. The combination of `Id`, `Kind` and `SubKind` should make the node unique among its siblings. This is very important.





### APITreeNode
- `Name (n)`  : The name of the tree node which will be used for API navigation.
- `Id (i)` : Id of the node, which should be unique at the node level. i.e. unique among its siblings
- `Kind (k)` : What kind of node is it. (namespace, class, module, method e.t.c)
- `Tags (t)` : Use this for opt in or opt out boolean properties e.g. `Deprecated`, `Hidden`, `HideFromNavigation`
- `Properties (p)` : Use this for other properties of the node. If the node needs more specification e.g. Use `SubKind` entry to make the node kind more specific. 
- `TopTokens (tt)` : The main data of the node.
- `BottomToken (bt)` : Data that closes out the node.
- `Children (c)` : Node immediate descendant

### StructuredToken
- `Value (v)` : The token value which will be dispalyed.
- `Id (i)` : Which will be used to navigate and find token on page.
- `Kind (k)` : Could be `Content` `LineBreak` `NoneBreakingSpace` `TabSpace` `ParameterSeparator` `Url`
  All tokens should be content except for spacing tokens and url. ParameterSeparator should be used between method or function parameters. Spacing token dont need to have value.
- `Tags (t)` : Use this for opt in or opt out boolean properties e.g. `SkippDiff`
- `Properties (p)` : Capture any other interesting data here. e.g Use `GroupId` : `doc` to group consecutive comment tokens.
- `RenderClasses (rc)` : Add css classes for how the tokens will be rendred. Classes currently being used are `text` `keyword` `punctuation` `type-name` `member-name` `literal` `string-literal` `comment` Feel free to add your own custom class. Whatever custom classes you use please provide us the appriopriate css for the class so we can update APIView.

Json property names are show in brackets.






If you want to have space between the API nodes add an empty token and lineBreak at the end of bottom tokens to simulate one empty line.

Dont worry about indentation that will be handeled by the tree structure, unless you want to have indentation between the tokens then use `TabSpace` token kind.

If your packages contains multiple assemblies then you will have multiple trees with multiple roots.
Assign the final parsed value to `APIForest` property of the `CodeFile`.

Serialize the generated code file to JSON. Try to make the json as small as possible by ignoring null values and empty collections, and using the abbreviated names of theproperties as the Json property name

## How to handle commons Scenarios
- TEXT, KEYWORD, COMMENT : Add `text`, `keyword`, `comment` to RenderClasses of the token
- NEW_LINE : Create a token with `Kind = LineBreak`
- WHITE_SPACE :  Create token with `Kind = NoneBreakingSpace`
- PUNCTUATION : Create a token with `Kind = Content` and the `Value = the punctuation`
- DOCUMENTATION : Add `GroupId = doc` in the properties of the token. This identifies a range of consecutive tokens as belonging to a group.
- SKIP_DIFF :  Add `SkipDiff` to the Tag to indicate that node or token should not be included in diff computation
- LINE_ID_MARKER : You can add a empty token. `Kind = Content` and `Value = ""` then give it an `Id` to make it commentable.
- EXTERNAL_LINK : Create a single token set `Kind = Url`, `Value = link` then add the link text as a properties `LinkText`;
- Common Tags: `Deprecated`, `Hidden`, `HideFromNav`, `SkipDiff`
- Cross Language Id: Use `CrossLangId` as key with value in the node properties.

Please reach out at [APIView Teams Channel](https://teams.microsoft.com/l/channel/19%3A3adeba4aa1164f1c889e148b1b3e3ddd%40thread.skype/APIView?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47) if you need more infomation.