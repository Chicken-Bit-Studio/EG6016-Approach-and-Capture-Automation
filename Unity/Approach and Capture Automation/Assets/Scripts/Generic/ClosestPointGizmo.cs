using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System;

public class ClosestPointGizmo : MonoBehaviour
{
    [Header("GameObjects")]
    public GameObject target;
    public GameObject[] subjects;

    [Header("Debug Options")]
    public bool logDetailedReport = false;

    private Collider[] targetColliders;

    private void Start()
    {
        CacheTargetColliders();
    }

    private void CacheTargetColliders()
    {
        if (target != null && subjects.Length > 0)
        {
            targetColliders = target.GetComponentsInChildren<Collider>();
            
            if (logDetailedReport)
            {
                UnityEngine.Debug.Log($"Found {targetColliders.Length} collider(s) on target '{target.name}'");
            }
        }
        else
        {
            this.enabled = false;
        }
    }

    private void OnDrawGizmos()
    {
        if (subjects == null || subjects.Length == 0 || target == null)
            return;

        // Recache if target colliders haven't been initialized
        if (targetColliders == null || targetColliders.Length == 0)
        {
            CacheTargetColliders();
        }

        if (targetColliders == null || targetColliders.Length == 0)
            return;

        Stopwatch stopwatch = null;
        
        if (logDetailedReport)
        {
            stopwatch = Stopwatch.StartNew();
        }

        // Store results for logging
        List<string> subjectResults = new List<string>();
        int totalCalculations = 0;

        // Process each subject
        foreach (GameObject subject in subjects)
        {
            if (subject == null)
                continue;

            Vector3 subjectOrigin = subject.transform.position;
            float closestDistance = float.MaxValue;
            Vector3 closestPoint = Vector3.zero;
            Collider closestCollider = null;

            // Find the closest point among all colliders for this subject
            foreach (Collider col in targetColliders)
            {
                if (col == null || !col.enabled)
                    continue;

                Vector3 pointOnCollider = col.ClosestPoint(subjectOrigin);
                float distance = Vector3.Distance(subjectOrigin, pointOnCollider);
                totalCalculations++;

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPoint = pointOnCollider;
                    closestCollider = col;
                }
            }

            // Draw the gizmo line for this subject
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(subjectOrigin, closestPoint);
            
            // Draw a small sphere at the closest point
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(closestPoint, 0.1f);

            // Store result for this subject
            if (logDetailedReport)
            {
                subjectResults.Add($"  • {subject.name} → Closest: {(closestCollider != null ? closestCollider.gameObject.name : "None")} | Point: {closestPoint} | Distance: {closestDistance:F3}");
            }
        }

        // Log the complete report for the whole round
        if (logDetailedReport && stopwatch != null)
        {
            stopwatch.Stop();
            
            string report = $"=== Closest Point Report (Full Round) ===\n" +
                $"Target: {target.name}\n" +
                $"Subjects Processed: {subjects.Length}\n" +
                $"Target Colliders: {targetColliders.Length}\n" +
                $"Total Distance Calculations: {totalCalculations}\n" +
                $"Results:\n" +
                string.Join("\n", subjectResults) + "\n" +
                $"Process Time: {stopwatch.Elapsed.TotalMilliseconds:F4} ms";
            
            UnityEngine.Debug.Log(report);
        }
    }
}