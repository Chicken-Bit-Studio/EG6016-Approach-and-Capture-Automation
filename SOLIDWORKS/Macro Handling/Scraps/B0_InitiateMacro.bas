Attribute VB_Name = "B0_InitiateMacro"
'Sub InitiateMacro()
'    Set swApp = Application.SldWorks                                                        ' Initiate the SOLIDWORKS environment
'    swApp.Visible = False                                                                   ' Hide the SOLIDWORKS window(s) that this macro opens
'    'modelProfile_path = Environ("OneDrive") & modelProfile_pathExtension                   ' Completing the modelProfile_path string
'    Call CheckForFolder(modelProfile_path)
'End Sub

' This is currently commented-out because it was throwing ambiguous name errors with InitiateMacro and swApp.
' Projects share a namespace, so be careful. Consider generaising A0_InitiateMacro and providing options at statup, or write a new project file entirely.
