#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

extern UIViewController *UnityGetGLViewController(void);
extern void UnitySendMessage(const char *objectName, const char *methodName, const char *message);

@interface WJAvatarPickerDelegate : NSObject<UINavigationControllerDelegate, UIImagePickerControllerDelegate>
@property(nonatomic, copy) NSString *receiverName;
@end

static WJAvatarPickerDelegate *WJAvatarPickerInstance;

static void WJSendAvatarMessage(NSString *receiverName, const char *methodName, NSString *message)
{
    UnitySendMessage(receiverName.UTF8String, methodName, (message ?: @"").UTF8String);
}

static UIImage *WJPreparedAvatarImage(UIImage *image, CGFloat maximumSide)
{
    if (image == nil) {
        return nil;
    }

    CGFloat largestSide = MAX(image.size.width, image.size.height);
    CGFloat scale = largestSide > maximumSide ? maximumSide / largestSide : 1.0;
    CGSize size = CGSizeMake(MAX(1.0, round(image.size.width * scale)), MAX(1.0, round(image.size.height * scale)));
    UIGraphicsBeginImageContextWithOptions(size, YES, 1.0);
    [image drawInRect:CGRectMake(0.0, 0.0, size.width, size.height)];
    UIImage *prepared = UIGraphicsGetImageFromCurrentImageContext();
    UIGraphicsEndImageContext();
    return prepared;
}

@implementation WJAvatarPickerDelegate

- (void)imagePickerControllerDidCancel:(UIImagePickerController *)picker
{
    NSString *receiverName = self.receiverName;
    [picker dismissViewControllerAnimated:YES completion:^{
        WJSendAvatarMessage(receiverName, "OnAvatarSelectionFailed", @"头像选择已取消");
        WJAvatarPickerInstance = nil;
    }];
}

- (void)imagePickerController:(UIImagePickerController *)picker didFinishPickingMediaWithInfo:(NSDictionary *)info
{
    UIImage *selected = info[UIImagePickerControllerEditedImage] ?: info[UIImagePickerControllerOriginalImage];
    UIImage *prepared = WJPreparedAvatarImage(selected, 1600.0);
    NSData *jpegData = nil;
    for (CGFloat quality = 0.9; quality >= 0.5; quality -= 0.1) {
        jpegData = UIImageJPEGRepresentation(prepared, quality);
        if (jpegData.length <= 1900 * 1024) {
            break;
        }
    }

    NSString *receiverName = self.receiverName;
    if (jpegData.length == 0) {
        [picker dismissViewControllerAnimated:YES completion:^{
            WJSendAvatarMessage(receiverName, "OnAvatarSelectionFailed", @"所选图片无法读取");
            WJAvatarPickerInstance = nil;
        }];
        return;
    }

    NSString *fileName = [NSString stringWithFormat:@"weijun-avatar-%@.jpg", NSUUID.UUID.UUIDString];
    NSString *filePath = [NSTemporaryDirectory() stringByAppendingPathComponent:fileName];
    NSError *writeError = nil;
    BOOL written = [jpegData writeToFile:filePath options:NSDataWritingAtomic error:&writeError];
    if (!written) {
        NSString *message = writeError.localizedDescription ?: @"头像临时文件保存失败";
        [picker dismissViewControllerAnimated:YES completion:^{
            WJSendAvatarMessage(receiverName, "OnAvatarSelectionFailed", message);
            WJAvatarPickerInstance = nil;
        }];
        return;
    }

    NSDictionary *payload = @{
        @"filePath": filePath,
        @"fileName": @"avatar.jpg",
        @"mimeType": @"image/jpeg",
        @"temporaryFile": @YES
    };
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:payload options:0 error:nil];
    NSString *json = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
    [picker dismissViewControllerAnimated:YES completion:^{
        WJSendAvatarMessage(receiverName, "OnAvatarSelected", json);
        WJAvatarPickerInstance = nil;
    }];
}

@end

extern "C" void WJ_LocalAvatarChoose(const char *receiverName)
{
    NSString *target = receiverName == NULL ? @"" : [NSString stringWithUTF8String:receiverName];
    dispatch_async(dispatch_get_main_queue(), ^{
        if (![UIImagePickerController isSourceTypeAvailable:UIImagePickerControllerSourceTypePhotoLibrary]) {
            WJSendAvatarMessage(target, "OnAvatarSelectionFailed", @"当前设备无法打开系统相册");
            return;
        }

        WJAvatarPickerInstance = [WJAvatarPickerDelegate new];
        WJAvatarPickerInstance.receiverName = target;
        UIImagePickerController *picker = [UIImagePickerController new];
        picker.sourceType = UIImagePickerControllerSourceTypePhotoLibrary;
        picker.mediaTypes = @[@"public.image"];
        picker.allowsEditing = YES;
        picker.delegate = WJAvatarPickerInstance;
        [UnityGetGLViewController() presentViewController:picker animated:YES completion:nil];
    });
}
