_for Windows_

This program registers global hotkeys listeners to run bound actions.
Hotkeys are specified in `hk.json` file. Here is an example file:

```json
{
  "alt+f1": "ontop.dll"
}
```

You can set these types of actions:

- executable file
- `dll` with entry point: `class Program { static void Main() {} }`
- `.script` file with the same entry point as in dll, which is a simply `.cs` file with a valid code

This was written a long time ago and may contain bugs. But it still works with a single key binding from the above example. It helps to quickly toggle on-top mode for any window. :)

See also <https://github.com/dhmk083/ontop>
