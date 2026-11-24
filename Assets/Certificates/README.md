# 书屿梦行局域网 CA

`weijun-game-lan-ca.pem.txt` 来自用户提供的 `weijun-game-局域网授权CA.pem`，仅包含根 CA 公钥证书，没有私钥。追加 `.txt` 是为了让 Unity 将 PEM 导入为 TextAsset，证书内容不变。此目录不是 Resources 或 StreamingAssets，未自动挂接网络请求或配置打包。

## 证书身份

- 类型：mkcert 开发根 CA，Basic Constraints 为 CA。
- 有效期（北京时间）：2026-08-07 13:48:07 至 2036-08-07 13:48:07。
- SHA-256：`1A0AB304F65253AB5A8FC7002E23D03C1BA53BC7AF9B1B3B2032EDF0460E9820`。
- Windows 证书指纹（SHA-1，用于定位，不作为签名算法）：`8A28E5DC8961F5FB83A736EA4D8290F7D4721F73`。
- 目标开发地址：`https://192.168.1.186/api`。

## 信任范围

把 CA 放进 Unity 项目不会自动建立 HTTPS 信任。Windows 开发机须将经过核验的根 CA 安装到当前用户的“受信任的根证书颁发机构”（`Cert:\CurrentUser\Root`），安装后重启 Unity Editor 再检查实际请求。信任该根 CA 会影响当前用户下依赖该证书库的应用，不仅限于这个 Unity 项目。

服务器必须使用该 CA 签发的有效服务器证书，SAN 必须包含 `IP:192.168.1.186`，并正确提供所需证书链。不得使用关闭校验或 `ValidateCertificate` 无条件返回 true 的方式处理连接失败。

其他电脑、手机和浏览器环境需分别配置其平台信任；本机安装不会自动传递给构建产物或测试设备。正式微信发布应使用合规域名及受公开 CA 信任的证书，不使用该局域网开发 CA。

## 核验与移除

可在 `certmgr.msc` 的“受信任的根证书颁发机构 / 证书”中，按上面的指纹核对安装记录；不再需要时只移除该指纹对应的证书，不删除其他受信任根证书。

健康检查应在保持证书验证启用的情况下运行：

```powershell
curl.exe --noproxy "*" --connect-timeout 5 --max-time 10 https://192.168.1.186/api/health
```

健康接口返回不代表 Unity 已完成运行验收，仍需在 Editor 中核对登录、数据读取和 WebSocket 连接。切勿将 CA 私钥或服务器私钥加入 Unity 或提交 Git。

## 本次核验记录（2026-09-14）

- 导入后的证书 DER SHA-256 与用户提供的原证书完全相同。
- 在实际 Windows 用户环境检查，`CurrentUser/Root` 已有上述同指纹 CA，因此未重复添加；`LocalMachine/Root` 中没有该证书，本次未扩大到计算机级信任库。
- 保持证书验证开启并直连 `https://192.168.1.186/api/health` 时，TCP 443 返回 `Connection refused`，没有进入 TLS 握手。服务器证书是否由此 CA 签发、IP SAN 是否匹配以及 Unity 实际连接仍待服务恢复后验证。
