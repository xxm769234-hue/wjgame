using System;

[Serializable]
public sealed class ApiResponse<T>
{
    public int code;
    public string message;
    public T data;
}

public sealed class BackendResult<T>
{
    public readonly bool Success;
    public readonly int HttpStatus;
    public readonly string Message;
    public readonly T Data;

    public BackendResult(bool success, int httpStatus, string message, T data)
    {
        Success = success;
        HttpStatus = httpStatus;
        Message = message;
        Data = data;
    }
}

[Serializable]
public sealed class EmptyResponse { }

[Serializable]
public sealed class LoginRequest
{
    public string username;
    public string password;
}

[Serializable]
public sealed class RegisterRequest
{
    public string username;
    public string password;
    public string nickname;
}

[Serializable]
public sealed class PlayerIdRequest
{
    public int playerId;
}

[Serializable]
public sealed class PlaceLandRequest
{
    public int playerId;
    public int landId;
    public int positionX;
    public int positionY;
}

[Serializable]
public sealed class BuildBuildingRequest
{
    public int playerId;
    public int buildingId;
    public int landId;
}

[Serializable]
public sealed class PlayerBuildingRequest
{
    public int playerId;
    public int playerBuildingId;
}

[Serializable]
public sealed class NpcSettleRequest
{
    public int playerId;
    public int playerBuildingId;
    public int playerNpcId;
}

[Serializable]
public sealed class MoveBuildingRequest
{
    public int playerId;
    public int playerBuildingId;
    public int targetLandId;
}

[Serializable]
public sealed class LoginResponse
{
    public string token;
    public int expiresIn;
    public UserResponse user;
}

[Serializable]
public sealed class UserResponse
{
    public int id;
    public string username;
    public string nickname;
    public int level;
    public int gold;
    public int wood;
    public int stone;
    public int iron;
    public InventoryItemResponse[] items;
}

[Serializable]
public sealed class InventoryItemResponse
{
    public int itemId;
    public string itemKey;
    public string name;
    public string itemType;
    public string icon;
    public int quantity;
}

[Serializable]
public sealed class MapDataResponse
{
    public int playerId;
    public int width;
    public int height;
    public LandTileResponse[] lands;
    public BuildingResponse[] buildings;
}

[Serializable]
public sealed class LandTileResponse
{
    public int playerLandId;
    public int landId;
    public string landType;
    public string landName;
    public string allowBuildType;
    public int level;
    public int positionX;
    public int positionY;
}

[Serializable]
public sealed class LandConfigResponse
{
    public int landId;
    public string landType;
    public string name;
    public string cost;
    public int unlockLevel;
    public string allowBuildType;
    public int goldCost;
    public int woodCost;
    public int stoneCost;
    public int ironCost;
    public bool canPlace;
    public string cannotPlaceReason;
}

[Serializable]
public sealed class BuildingConfigResponse
{
    public int id;
    public int buildingId;
    public string code;
    public string name;
    public string description;
    public string needLand;
    public string cost;
    public string produce;
    public string buildType;
    public string type;
    public int level;
    public int goldCost;
    public int woodCost;
    public int stoneCost;
    public int ironCost;
    public int buildTime;
    public string modelKey;
    public string icon;
    public int requiredLevel;
    public int prerequisiteBuildingId;
    public bool canBuild;
    public string cannotBuildReason;
}

[Serializable]
public sealed class BuildingConditionResponse
{
    public int buildingId;
    public bool canBuild;
    public bool levelMet;
    public bool resourceMet;
    public bool prerequisiteMet;
    public bool landMet;
    public string message;
    public int goldNeeded;
    public int woodNeeded;
    public int stoneNeeded;
    public int ironNeeded;
    public int requiredLevel;
    public string missingPrerequisite;
}

[Serializable]
public sealed class BuildingResponse
{
    public int playerBuildingId;
    public int buildingId;
    public int landId;
    public string buildingCode;
    public string buildingName;
    public string buildingType;
    public int level;
    public string produce;
    public string modelKey;
    public string icon;
    public string status;
    public int positionX;
    public int positionY;
    public string buildStartTime;
    public string buildEndTime;
    public int remainingTime;
    public float progress;
}

[Serializable]
public sealed class ProductionResponse
{
    public int playerBuildingId;
    public int landId;
    public int buildingId;
    public string buildingName;
    public int level;
    public string status;
    public int itemId;
    public string itemKey;
    public string itemName;
    public string icon;
    public int baseProduceAmount;
    public int produceAmount;
    public float npcProduceBonusRate;
    public SettledNpcResponse settledNpc;
    public int produceIntervalSeconds;
    public int storedAmount;
    public int storageCapacity;
    public bool canCollect;
    public string lastSettleTime;
    public string nextProduceTime;
    public int remainingSeconds;
}

[Serializable]
public sealed class NpcConfigResponse
{
    public int npcId;
    public string npcCode;
    public string name;
    public string icon;
    public string modelKey;
    public float produceBonusRate;
}

[Serializable]
public sealed class NpcResponse
{
    public int playerNpcId;
    public int npcId;
    public string npcCode;
    public string name;
    public string icon;
    public string modelKey;
    public int level;
    public string state;
    public int settledPlayerBuildingId;
    public float produceBonusRate;
}

[Serializable]
public sealed class SettledNpcResponse
{
    public int playerNpcId;
    public int npcId;
    public string npcCode;
    public string name;
    public string icon;
    public string modelKey;
    public float produceBonusRate;
}

[Serializable]
public sealed class ProductionCollectResponse
{
    public int playerId;
    public CollectedItemResponse[] collectedItems;
    public ProductionResponse[] productions;
    public InventoryItemResponse[] items;
}

[Serializable]
public sealed class CollectedItemResponse
{
    public int itemId;
    public string itemKey;
    public string name;
    public int quantity;
}
