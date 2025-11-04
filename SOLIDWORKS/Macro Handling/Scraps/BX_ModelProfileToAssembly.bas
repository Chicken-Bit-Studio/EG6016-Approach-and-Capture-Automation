Attribute VB_Name = "BX_ModelProfileToAssembly"
' Intended for use with the second iteration of my personal robotics project.
' Status: ???
' Author: Ben Searle
'''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

' input:    what. a modelProfile.json file address. how. file reads.

' output:   An open solidworks assembly

Public swApp As SldWorks.SldWorks
Public Const modelProfile_path = "C:\Users\bense\OneDrive - Kingston University\EG6016 Individual Project\2025_26\Solidworks"

Sub main()

    Call InitiateMacro
    
    '
    ' open new assembly
    '
    
    MsgBox "Macro completed successfully."

End Sub
