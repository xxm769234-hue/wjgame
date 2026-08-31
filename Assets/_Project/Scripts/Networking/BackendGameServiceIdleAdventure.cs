using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class BackendGameService
{
    public IdleAdventureModeConfigResponse IdleAdventureConfig { get; private set; }
    public IdleAdventureBuffPresetResponse[] IdleAdventureBuffPresets { get; private set; }
    public IdleAdventureSkillDefinitionResponse[] IdleAdventureSkills { get; private set; }
    public IdleAdventureFieldBuffResponse[] IdleAdventureFieldBuffs { get; private set; }
    public IdleAdventureAttributeRestraintConfigResponse IdleAdventureAttributeRestraints { get; private set; }
    public IdleAdventureStageRewardResponse[] IdleAdventureStageRewards { get; private set; }
    public IdleAdventurePlayerNpcResponse[] IdleAdventureAvailableNpcs { get; private set; }
    public IdleAdventureStageEnemiesResponse IdleAdventureStageEnemyPreview { get; private set; }
    public IdleAdventureProgressResponse IdleAdventureProgress { get; private set; }

    public Coroutine LoadIdleAdventureConfig(
        Action<BackendResult<IdleAdventureModeConfigResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<IdleAdventureModeConfigResponse>(
                "Backend session is not authenticated."));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureReferenceEndpointRoutine(
            "idle-adventure/config",
            value => IdleAdventureConfig = value,
            onComplete,
            "Idle Adventure config"));
    }

    public Coroutine LoadIdleAdventureBuffPresets(
        Action<BackendResult<IdleAdventureBuffPresetResponse[]>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<IdleAdventureBuffPresetResponse[]>(
                "Backend session is not authenticated."));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureReferenceEndpointRoutine(
            "idle-adventure/buff-presets",
            value => IdleAdventureBuffPresets = value,
            onComplete,
            "Idle Adventure buff presets"));
    }

    public Coroutine LoadIdleAdventureSkills(
        Action<BackendResult<IdleAdventureSkillDefinitionResponse[]>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<IdleAdventureSkillDefinitionResponse[]>(
                "Backend session is not authenticated."));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureReferenceEndpointRoutine(
            "idle-adventure/skills",
            value => IdleAdventureSkills = value,
            onComplete,
            "Idle Adventure skills"));
    }

    public Coroutine LoadIdleAdventureFieldBuffs(
        int? stage = null,
        Action<BackendResult<IdleAdventureFieldBuffResponse[]>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<IdleAdventureFieldBuffResponse[]>(
                "Backend session is not authenticated."));
            return null;
        }

        string path = "idle-adventure/field-buffs";
        if (stage.HasValue)
        {
            if (stage.Value <= 0)
            {
                onComplete?.Invoke(Failed<IdleAdventureFieldBuffResponse[]>(
                    "Idle Adventure stage must be positive."));
                return null;
            }

            path += "?stage=" + stage.Value;
        }

        return StartCoroutine(LoadIdleAdventureReferenceEndpointRoutine(
            path,
            value => IdleAdventureFieldBuffs = value,
            onComplete,
            "Idle Adventure field buffs"));
    }

    public Coroutine LoadIdleAdventureFieldBuffs(
        Action<BackendResult<IdleAdventureFieldBuffResponse[]>> onComplete)
    {
        return LoadIdleAdventureFieldBuffs(null, onComplete);
    }

    public Coroutine LoadIdleAdventureAttributeRestraints(
        Action<BackendResult<IdleAdventureAttributeRestraintConfigResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<IdleAdventureAttributeRestraintConfigResponse>(
                "Backend session is not authenticated."));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureReferenceEndpointRoutine(
            "idle-adventure/attribute-restraints",
            value => IdleAdventureAttributeRestraints = value,
            onComplete,
            "Idle Adventure attribute restraints"));
    }

    public Coroutine LoadIdleAdventureStageRewards(
        Action<BackendResult<IdleAdventureStageRewardResponse[]>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<IdleAdventureStageRewardResponse[]>(
                "Backend session is not authenticated."));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureReferenceEndpointRoutine(
            "idle-adventure/stage-rewards",
            value => IdleAdventureStageRewards = value,
            onComplete,
            "Idle Adventure stage rewards"));
    }

    public Coroutine LoadIdleAdventurePlayerNpcs(
        Action<BackendResult<IdleAdventurePlayerNpcResponse[]>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<IdleAdventurePlayerNpcResponse[]>(
                "Backend session is not authenticated."));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureReferenceEndpointRoutine(
            $"idle-adventure/players/{PlayerId}/npcs",
            value => IdleAdventureAvailableNpcs = value,
            onComplete,
            "Idle Adventure player NPCs"));
    }

    public Coroutine LoadIdleAdventureStageEnemies(
        int stage,
        Action<BackendResult<IdleAdventureStageEnemiesResponse>> onComplete = null)
    {
        if (!IsAuthenticated || stage <= 0)
        {
            onComplete?.Invoke(Failed<IdleAdventureStageEnemiesResponse>(
                !IsAuthenticated
                    ? "Backend session is not authenticated."
                    : "Idle Adventure stage must be positive."));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureReferenceEndpointRoutine(
            $"idle-adventure/stages/{stage}/enemies",
            value => IdleAdventureStageEnemyPreview = value,
            onComplete,
            "Idle Adventure stage enemies"));
    }

    public Coroutine RefreshIdleAdventureProgress(
        Action<BackendResult<IdleAdventureProgressResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(Failed<IdleAdventureProgressResponse>(
                "Backend session is not authenticated."));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureReferenceEndpointRoutine(
            $"idle-adventure/players/{PlayerId}/progress",
            ApplyIdleAdventureProgressSnapshot,
            onComplete,
            "Idle Adventure progress"));
    }

    public Coroutine RefreshIdleAdventureReferenceData(
        int stage,
        Action<BackendResult<IdleAdventureReferenceDataResponse>> onComplete = null)
    {
        if (!IsAuthenticated || stage <= 0)
        {
            onComplete?.Invoke(Failed<IdleAdventureReferenceDataResponse>(
                !IsAuthenticated
                    ? "Backend session is not authenticated."
                    : "Idle Adventure stage must be positive."));
            return null;
        }

        return StartCoroutine(RefreshIdleAdventureReferenceDataRoutine(stage, onComplete));
    }

    private IEnumerator RefreshIdleAdventureReferenceDataRoutine(
        int stage,
        Action<BackendResult<IdleAdventureReferenceDataResponse>> onComplete)
    {
        BackendResult<IdleAdventureModeConfigResponse> configResult = null;
        BackendResult<IdleAdventureBuffPresetResponse[]> buffResult = null;
        BackendResult<IdleAdventureSkillDefinitionResponse[]> skillResult = null;
        BackendResult<IdleAdventureFieldBuffResponse[]> fieldBuffResult = null;
        BackendResult<IdleAdventureAttributeRestraintConfigResponse> restraintResult = null;
        BackendResult<IdleAdventureStageRewardResponse[]> rewardResult = null;
        BackendResult<IdleAdventurePlayerNpcResponse[]> npcResult = null;
        BackendResult<IdleAdventureStageEnemiesResponse> enemyResult = null;

        yield return LoadIdleAdventureConfig(value => configResult = value);
        yield return LoadIdleAdventureBuffPresets(value => buffResult = value);
        yield return LoadIdleAdventureSkills(value => skillResult = value);
        yield return LoadIdleAdventureFieldBuffs(stage, value => fieldBuffResult = value);
        yield return LoadIdleAdventureAttributeRestraints(value => restraintResult = value);
        yield return LoadIdleAdventureStageRewards(value => rewardResult = value);
        yield return LoadIdleAdventurePlayerNpcs(value => npcResult = value);
        yield return LoadIdleAdventureStageEnemies(stage, value => enemyResult = value);

        IdleAdventureReferenceDataResponse data = new IdleAdventureReferenceDataResponse
        {
            config = configResult != null && configResult.Success ? configResult.Data : null,
            buff_presets = buffResult != null && buffResult.Success ? buffResult.Data : null,
            skills = skillResult != null && skillResult.Success ? skillResult.Data : null,
            field_buffs = fieldBuffResult != null && fieldBuffResult.Success ? fieldBuffResult.Data : null,
            attribute_restraints = restraintResult != null && restraintResult.Success
                ? restraintResult.Data
                : null,
            stage_rewards = rewardResult != null && rewardResult.Success ? rewardResult.Data : null,
            player_npcs = npcResult != null && npcResult.Success ? npcResult.Data : null,
            stage_enemies = enemyResult != null && enemyResult.Success ? enemyResult.Data : null
        };

        BackendResult<IdleAdventureReferenceDataResponse> aggregate =
            CreateIdleAdventureReferenceAggregateResult(
                data,
                configResult,
                buffResult,
                skillResult,
                fieldBuffResult,
                restraintResult,
                rewardResult,
                npcResult,
                enemyResult);
        onComplete?.Invoke(aggregate);
    }

    private static BackendResult<IdleAdventureReferenceDataResponse> CreateIdleAdventureReferenceResult(
        bool success,
        int status,
        int code,
        string message,
        IdleAdventureReferenceDataResponse data)
    {
        return new BackendResult<IdleAdventureReferenceDataResponse>(
            success,
            status,
            code,
            string.IsNullOrEmpty(message) ? "Idle Adventure reference data loaded." : message,
            data);
    }

    private static BackendResult<IdleAdventureReferenceDataResponse> CreateIdleAdventureReferenceAggregateResult(
        IdleAdventureReferenceDataResponse data,
        params object[] results)
    {
        bool allSucceeded = true;
        int status = 200;
        int code = 200;
        List<string> failures = new List<string>();

        for (int i = 0; i < results.Length; i++)
        {
            object value = results[i];
            if (value == null) {
                allSucceeded = false;
                failures.Add("missing response");
                continue;
            }

            bool success;
            int responseStatus;
            int responseCode;
            string message;
            if (value is BackendResult<IdleAdventureModeConfigResponse> config)
            {
                success = config.Success;
                responseStatus = config.HttpStatus;
                responseCode = config.Code;
                message = config.Message;
            }
            else if (value is BackendResult<IdleAdventureBuffPresetResponse[]> buffs)
            {
                success = buffs.Success;
                responseStatus = buffs.HttpStatus;
                responseCode = buffs.Code;
                message = buffs.Message;
            }
            else if (value is BackendResult<IdleAdventureSkillDefinitionResponse[]> skills)
            {
                success = skills.Success;
                responseStatus = skills.HttpStatus;
                responseCode = skills.Code;
                message = skills.Message;
            }
            else if (value is BackendResult<IdleAdventureFieldBuffResponse[]> fields)
            {
                success = fields.Success;
                responseStatus = fields.HttpStatus;
                responseCode = fields.Code;
                message = fields.Message;
            }
            else if (value is BackendResult<IdleAdventureAttributeRestraintConfigResponse> restraints)
            {
                success = restraints.Success;
                responseStatus = restraints.HttpStatus;
                responseCode = restraints.Code;
                message = restraints.Message;
            }
            else if (value is BackendResult<IdleAdventureStageRewardResponse[]> rewards)
            {
                success = rewards.Success;
                responseStatus = rewards.HttpStatus;
                responseCode = rewards.Code;
                message = rewards.Message;
            }
            else if (value is BackendResult<IdleAdventurePlayerNpcResponse[]> npcs)
            {
                success = npcs.Success;
                responseStatus = npcs.HttpStatus;
                responseCode = npcs.Code;
                message = npcs.Message;
            }
            else if (value is BackendResult<IdleAdventureStageEnemiesResponse> enemies)
            {
                success = enemies.Success;
                responseStatus = enemies.HttpStatus;
                responseCode = enemies.Code;
                message = enemies.Message;
            }
            else
            {
                success = false;
                responseStatus = 0;
                responseCode = 0;
                message = "unknown response";
            }

            if (responseStatus > 0) status = responseStatus;
            if (responseCode > 0) code = responseCode;
            if (!success)
            {
                allSucceeded = false;
                if (!string.IsNullOrEmpty(message)) failures.Add(message);
            }
        }

        return CreateIdleAdventureReferenceResult(
            allSucceeded,
            status,
            code,
            failures.Count == 0 ? "Idle Adventure reference data loaded." : string.Join("; ", failures),
            data);
    }

    private IEnumerator LoadIdleAdventureReferenceEndpointRoutine<T>(
        string path,
        Action<T> apply,
        Action<BackendResult<T>> onComplete,
        string description)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        BackendResult<T> result = null;
        yield return BackendApiClient.Get<T>(
            baseUrl,
            session.Token,
            path,
            value => result = value);

        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(Failed<T>(
                "Backend session changed while " + description + " was loading."));
            yield break;
        }

        if (result != null && result.Success && result.Data != null)
        {
            apply?.Invoke(result.Data);
        }

        onComplete?.Invoke(result);
    }

    private void ApplyIdleAdventureProgressSnapshot(IdleAdventureProgressResponse progress)
    {
        if (progress == null) return;

        IdleAdventureProgress = progress;
        int highestStage = Mathf.Max(0, progress.ResolvedHighestStage);
        PlayerPrefs.SetInt("weijun.idleAdventure.highestClearedStage", highestStage);
        PlayerPrefs.Save();

        IdleAdventureAfkRewardSource[] sources = FindObjectsByType<IdleAdventureAfkRewardSource>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null) sources[i].SetHighestClearedStageFromProgress(highestStage);
        }
    }
}
