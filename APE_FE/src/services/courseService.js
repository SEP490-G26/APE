import { http } from "./http";

const ADMIN_COURSES_BASE = "/api/admin/courses";

function normalizeCourse(course) {
  if (!course) {
    return course;
  }

  const id =
    course.id ??
    course.Id ??
    course._id?.$oid ??
    course._id ??
    "";
  const code = course.code ?? course.Code ?? "";
  const name = course.name ?? course.Name ?? "";
  const description = course.description ?? course.Description ?? "";
  const rawStatus = course.status ?? course.Status ?? (course.isActive ? "Active" : "Inactive");
  const status = typeof rawStatus === "string" ? rawStatus : "Inactive";
  const isActive = status.toLowerCase() === "active";

  return {
    ...course,
    id,
    code,
    name,
    description,
    status,
    isActive
  };
}

export async function getCourses(params = {}) {
  const query = new URLSearchParams();

  if (params.page) {
    query.set("page", params.page);
  }

  if (params.limit) {
    query.set("limit", params.limit);
  }

  if (params.status && params.status !== "all") {
    const normalizedStatus = params.status.toLowerCase() === "active" ? "Active" : "Inactive";
    query.set("status", normalizedStatus);
  }

  const suffix = query.toString() ? `?${query.toString()}` : "";
  const payload = await http(`${ADMIN_COURSES_BASE}${suffix}`);
  const data = payload?.data || payload;

  if (Array.isArray(data?.items)) {
    return {
      ...data,
      items: data.items.map(normalizeCourse)
    };
  }

  return data;
}

export async function getCourseById(courseId) {
  const payload = await http(`${ADMIN_COURSES_BASE}/${courseId}`);
  return normalizeCourse(payload?.data || payload);
}

export async function createCourse(course) {
  const payload = await http(ADMIN_COURSES_BASE, {
    method: "POST",
    body: JSON.stringify(course)
  });

  return normalizeCourse(payload?.data || payload);
}

export async function updateCourse(courseId, updates) {
  const payloadBody = {
    ...(updates.name !== undefined ? { name: updates.name } : {}),
    ...(updates.description !== undefined ? { description: updates.description } : {}),
    ...(updates.status !== undefined ? { status: updates.status } : {})
  };
  const payload = await http(`${ADMIN_COURSES_BASE}/${courseId}`, {
    method: "PUT",
    body: JSON.stringify(payloadBody)
  });

  return normalizeCourse(payload?.data || payload);
}

export async function deleteCourse(courseId) {
  await http(`${ADMIN_COURSES_BASE}/${courseId}`, {
    method: "DELETE"
  });
}

export async function activateCourse(courseId) {
  const payload = await http(`${ADMIN_COURSES_BASE}/${courseId}/status`, {
    method: "PATCH",
    body: JSON.stringify({ status: "Active" })
  });

  return normalizeCourse(payload?.data || payload);
}

export async function deactivateCourse(courseId) {
  const payload = await http(`${ADMIN_COURSES_BASE}/${courseId}/status`, {
    method: "PATCH",
    body: JSON.stringify({ status: "Inactive" })
  });

  return normalizeCourse(payload?.data || payload);
}
