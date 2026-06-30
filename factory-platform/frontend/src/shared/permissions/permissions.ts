export function canAccess(required: string[], userPermissions: string[]) {
  if (required.length === 0) {
    return true;
  }

  return required.every((permission) => userPermissions.includes(permission));
}
