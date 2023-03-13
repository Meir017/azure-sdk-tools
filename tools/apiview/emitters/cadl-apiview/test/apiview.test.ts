import assert, { fail, strictEqual } from "assert";
import { ApiViewDocument, ApiViewTokenKind } from "../src/apiview.js";
import { apiViewFor, apiViewText, compare } from "./test-host.js";

describe("apiview: tests", () => {
  /** Validates that there are no repeat defintion IDs and that each line has only one definition ID. */
  function validateDefinitionIds(apiview: ApiViewDocument) {
    const definitionIds = new Set<string>();
    const defIdsPerLine = new Array<Array<string>>();
    let index = 0;
    defIdsPerLine[index] = new Array<string>();
    for (const token of apiview.Tokens) {
      // ensure that there are no repeated definition IDs.
      if (token.DefinitionId != undefined) {
        if (definitionIds.has(token.DefinitionId)) {
          fail(`Duplicate defintion ID ${token.DefinitionId}.`);
        }
        definitionIds.add(token.DefinitionId);
      }
      // Collect the definition IDs that exist on each line
      if (token.DefinitionId != undefined) {
        defIdsPerLine[index].push(token.DefinitionId);
      }
      if (token.Kind == ApiViewTokenKind.Newline) {
        index++;
        defIdsPerLine[index] = new Array<string>();
      }
    }
    // ensure that each line has either 0 or 1 definition ID.
    for (let x = 0; x < defIdsPerLine.length; x++) {
      const row = defIdsPerLine[x];
      assert(row.length == 0 || row.length == 1, `Too many definition IDs (${row.length}) on line ${x}`);
    }
  }

  it("describes model", async () => {
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      model Animal {
        species: string;
      }

      model Pet {
        name?: string;
      }

      model Dog {
        ...Animal;
        ...Pet;
      }

      model Cat {
        species: string;
        name?: string = "fluffy";
      }

      model Pig extends Animal {}
    }
    `;
    const expect = `
    namespace Azure.Test {
      model Animal {
        species: string;
      }

      model Cat {
        species: string;
        name?: string = "fluffy";
      }

      model Dog {
        ...Animal;
        ...Pet;
      }

      model Pet {
        name?: string;
      }

      model Pig extends Animal {}
    }
    `;
    const apiview = await apiViewFor(input, {});
    const actual = apiViewText(apiview);
    compare(expect, actual, 9);
    validateDefinitionIds(apiview);
  });

  it("describes scalar", async () => {
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      scalar Password extends string;

      scalar ternary;

      @doc(T)
      scalar Unreal<T extends string>;
    }
    `;
    const expect = `
    namespace Azure.Test {
      scalar Password extends string

      scalar ternary
      @doc(T)
      scalar Unreal<T extends string>
    }
    `;
    const apiview = await apiViewFor(input, {});
    const actual = apiViewText(apiview);
    compare(expect, actual, 9);
    validateDefinitionIds(apiview);
  });

  it("describes alias", async () => {
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      model Animal {
        species: string;
      }

      alias Creature = Animal;
    }
    `;
    const expect = `
    namespace Azure.Test {
      model Animal {
        species: string;
      }

      alias Creature = Animal
    }
    `;
    const apiview = await apiViewFor(input, {});
    const actual = apiViewText(apiview);
    compare(expect, actual, 9);
    validateDefinitionIds(apiview);
  });

  it("describes augment decorator", async () => {
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      model Animal {
        species: string;
      }

      @@doc(Animal, "My doc")
    }
    `;
    const expect = `
    namespace Azure.Test {
      @@doc(Animal, "My doc")

      model Animal {
        species: string;
      }
    }
    `;
    const apiview = await apiViewFor(input, {});
    const actual = apiViewText(apiview);
    compare(expect, actual, 9);
    validateDefinitionIds(apiview);
  });


  it("describes templated model", async () => {
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      model Thing<T> {
        property: T;
      }

      model StringThing is Thing<string>;

      model Page<T = string> {
        size: int16;
        item: T[];
      }

      model StringPage {
        ...Page<int16>;
      }

      model ConstrainedSimple<X extends string> {
        prop: X;
      }

      model ConstrainedComplex<X extends {name: string}> {
        prop: X;
      }
      
      model ConstrainedWithDefault<X extends string = "abc"> {
        prop: X;
      }
    }
    `;
    const expect = `
    namespace Azure.Test {
      model ConstrainedComplex<X extends
        {
          name: string;
        }
      > {
        prop: X;
      }

      model ConstrainedSimple<X extends string> {
        prop: X;
      }

      model ConstrainedWithDefault<X extends string = "abc"> {
        prop: X;
      }

      model Page<T = string> {
        size: int16;
        item: T[];
      }

      model StringPage {
        ...Page<int16>;
      }

      model StringThing is Thing<string> {}

      model Thing<T> {
        property: T;
      }
    }
    `;
    const apiview = await apiViewFor(input, {});
    const actual = apiViewText(apiview);
    compare(expect, actual, 9);
    validateDefinitionIds(apiview);
  });

  it("describes enum", async () => {
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {

      enum SomeEnum {
        Plain,
        "Literal",
      }

      enum SomeStringEnum {
        A: "A",
        B: "B",
      }

      enum SomeIntEnum {
        A: 1,
        B: 2,
      }
    }`;
    const expect = `
    namespace Azure.Test {
      enum SomeEnum {
        Plain,
        "Literal",
      }

      enum SomeIntEnum {
        A: 1,
        B: 2,
      }

      enum SomeStringEnum {
        A: "A",
        B: "B",
      }
    }`;
    const apiview = await apiViewFor(input, {});
    const actual = apiViewText(apiview);
    compare(expect, actual, 9);
    validateDefinitionIds(apiview);
  });

  it("describes union", async () =>{
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      union MyUnion {
        cat: Cat,
        dog: Dog,
        snake: Snake
      }

      model Cat {
        name: string;
      }

      model Dog {
        name: string;
      }

      model Snake {
        name: string;
        length: int16;
      }
    }`;
    const expect = `
    namespace Azure.Test {
      model Cat {
        name: string;
      }

      model Dog {
        name: string;
      }

      union MyUnion {
        cat: Cat,
        dog: Dog,
        snake: Snake
      }

      model Snake {
        name: string;
        length: int16;
      }
    }
    `;
    const apiview = await apiViewFor(input, {});
    const actual = apiViewText(apiview);
    compare(expect, actual, 9);
    validateDefinitionIds(apiview);
  });

  it("describes template operation", async () =>{
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      model FooParams {
        a: string;
        b: string;
      }

      op ResourceRead<TResource, TParams>(resource: TResource, params: TParams): TResource;

      op GetFoo is ResourceRead<
        {
          @query
          @doc("The name")
          name: string,
          ...FooParams
        },
        {
          parameters: {
            @query
            @doc("The collection id.")
            fooId: string
          };
        }
      >;
    }`;
    const expect = `
    namespace Azure.Test {
      op GetFoo is ResourceRead<
        {
          @query
          @doc("The name")
          name: string;
          ...FooParams;
        },
        {
          parameters:
            {
              @query
              @doc("The collection id.")
              fooId: string;
            };
        }
      >;

      op ResourceRead<TResource, TParams>(
        resource: TResource,
        params: TParams
      ): TResource;

      model FooParams {
        a: string;
        b: string;
      }
    }`;
    const apiview = await apiViewFor(input, {});
    const lines = apiViewText(apiview);
    compare(expect, lines, 9);
    validateDefinitionIds(apiview);
  });

  it("describes operation with anonymous models", async () =>{
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      op SomeOp(
        param1: {
          name: string
        },
        param2: {
          age: int16
        }
      ): string;
    }`;
    const expect = `
    namespace Azure.Test {
      op SomeOp(
        param1:
          {
            name: string;
          },
        param2:
          {
            age: int16;
          }
      ): string;
    }`;
    const apiview = await apiViewFor(input, {});
    const lines = apiViewText(apiview);
    compare(expect, lines, 9);
    validateDefinitionIds(apiview);
  });

  it("describes interface", async () => {
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      interface Foo {
        @get 
        @route("get/{name}")
        get(@path name: string): string;


        @get
        @route("list")
        list(): string[];
      }
    }
    `;
    const expect = `
    namespace Azure.Test {
      interface Foo {
        @get
        @route("get/{name}")
        get(
          @path
          name: string
        ): string;
        @get
        @route("list")
        list(): string[];
      }
    }
    `;
    const apiview = await apiViewFor(input, {});
    const lines = apiViewText(apiview);
    compare(expect, lines, 9);
    validateDefinitionIds(apiview);
  });

  it("describes string literals", async () => {
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      @doc("Short string")
      model Foo {};

      @doc("""
      A long string,
      with line breaks
      and stuff...
      """)
      model Bar {};
    }
    `;
    const expect = `
    namespace Azure.Test {
      @doc("""
      A long string,
      with line breaks
      and stuff...
      """)
      model Bar {}
      @doc("Short string")
      model Foo {}
    }
    `;
    const apiview = await apiViewFor(input, {});
    const lines = apiViewText(apiview);
    compare(expect, lines, 9);
    validateDefinitionIds(apiview);
  });
});