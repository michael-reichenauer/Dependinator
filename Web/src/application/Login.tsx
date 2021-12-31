import React, { FC } from "react";
import { useState } from "react";
import { atom, useAtom } from "jotai";
import {
  Box,
  Button,
  Dialog,
  FormControlLabel,
  LinearProgress,
  Switch,
  Typography,
} from "@material-ui/core";
import { Formik, Form, Field } from "formik";
import { TextField } from "formik-material-ui";

const usernameKey = "credential.userName";
const loginAtom = atom(false);
export const useLogin = () => useAtom(loginAtom);

export const Login: FC = () => {
  const [show, setShow] = useLogin();
  const [createAccount, setCreateAccount] = useState(false);

  const handleEnter = (event: any): void => {
    if (event.code === "Enter") {
      const okButton = document.getElementById("OKButton");
      okButton?.click();
    }
  };

  return (
    <Dialog
      open={show}
      onClose={() => {
        setShow(false);
      }}
    >
      <Box style={{ width: 320, height: 330, padding: 20 }}>
        {!createAccount && (
          <Typography variant="h5" style={{ paddingBottom: 10 }}>
            Login
          </Typography>
        )}
        {createAccount && (
          <Typography variant="h5" style={{ paddingBottom: 10 }}>
            Create a new Account
          </Typography>
        )}

        <Formik
          initialValues={{
            username: getDefaultUserName(),
            password: "",
            confirm: "",
            create: false,
          }}
          validate={async (values) => {
            const errors: any = {};
            if (!values.username) {
              errors.username = "Required";
            }
            if (!values.password) {
              errors.password = "Required";
            }
            if (createAccount && values.password !== values.confirm) {
              errors.confirm = "Does not match password";
            }
            return errors;
          }}
          onSubmit={async (values, { setErrors, setFieldValue }) => {
            if (createAccount) {
              try {
                // await authenticate.createUser({
                //   username: values.username,
                //   password: values.password,
                // });
                setDefaultUserName(values.username);
                setCreateAccount(false);
                setFieldValue("confirm", "", false);
              } catch (error) {
                setFieldValue("password", "", false);
                setFieldValue("confirm", "", false);
                setErrors({ username: "User already exist" });
                return;
              }
            }

            try {
              console.log("connect", values);
              // await authenticate.connectUser({
              //   username: values.username,
              //   password: values.password,
              // });
              setDefaultUserName(values.username);
            } catch (error) {
              setFieldValue("password", "", false);
              setErrors({ username: "Invalid username or password" });
            }
          }}
        >
          {({ submitForm, isSubmitting }) => (
            <Form onKeyUp={handleEnter}>
              {isSubmitting && <LinearProgress style={{ marginBottom: 10 }} />}
              <Field
                label="Username"
                component={TextField}
                name="username"
                type="text"
                fullWidth={true}
              />
              <br />
              <Field
                label="Password"
                component={TextField}
                type="password"
                name="password"
                fullWidth={true}
              />
              <br />
              {createAccount && (
                <Field
                  label="Confirm"
                  component={TextField}
                  type="password"
                  name="confirm"
                  fullWidth={true}
                />
              )}

              <br />
              <FormControlLabel
                style={{ position: "absolute", top: 260 }}
                label="Create a new account"
                control={
                  <Field
                    component={Switch}
                    type="checkbox"
                    name="create"
                    color="primary"
                    onChange={(e: any) => setCreateAccount(e.target.checked)}
                  />
                }
              />

              <Box style={{ position: "absolute", top: 300, left: 80 }}>
                <Button
                  id="OKButton"
                  variant="contained"
                  color="primary"
                  disabled={isSubmitting}
                  onClick={submitForm}
                  style={{ margin: 5, width: 80 }}
                >
                  OK
                </Button>

                <Button
                  variant="contained"
                  color="primary"
                  disabled={isSubmitting}
                  onClick={() => setShow(false)}
                  style={{ margin: 5, width: 85 }}
                >
                  Cancel
                </Button>
              </Box>
            </Form>
          )}
        </Formik>
      </Box>
    </Dialog>
  );
};

const getDefaultUserName = () => localStorage.getItem(usernameKey) ?? "";

const setDefaultUserName = (name: string) =>
  localStorage.setItem(usernameKey, name);
