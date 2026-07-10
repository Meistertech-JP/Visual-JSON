' SPDX-License-Identifier: MPL-2.0
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Several suites write real files under %TEMP%/%LocalAppData% (settings, logs, recovery,
' save/backup); parallel classes would race on that shared state, so keep execution sequential.
<Assembly: DoNotParallelize()>
